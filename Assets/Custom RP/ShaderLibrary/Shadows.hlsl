#ifndef CUSTOM_SHADOWS_INCLUDED
#define CUSTOM_SHADOWS_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Shadow/ShadowSamplingTent.hlsl" //阴影采样函数库

//使用封装好的函数来减少采样次数，以提高性能
#if defined(_DIRECTIONAL_PCF3)
	#define DIRECTIONAL_FILTER_SAMPLES 4 //采样次数
	#define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_3x3 //帐篷过滤器设置
#elif defined(_DIRECTIONAL_PCF5)
	#define DIRECTIONAL_FILTER_SAMPLES 9
	#define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_5x5
#elif defined(_DIRECTIONAL_PCF7)
	#define DIRECTIONAL_FILTER_SAMPLES 16
	#define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_7x7
#endif


#define MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT 4 //定义最大阴影方向光数量
#define MAX_CASCADE_COUNT 4	//定义最大阴影级联数量

/*
	Unity中采样器有三个模式：Filter（过滤）、Wrap（寻址）、Compare（比较）
	创建采样器时Unity会自己推理该使用何种模式，你也可以手动控制，根据顺序对采样器命名即可
	但是阴影图集只有一种模式能够正常采样，所以我们直接指定（双线性插值_钳制_比较）
*/
TEXTURE2D_SHADOW(_DirectionalShadowAtlas); //使用TEXTURE2D_SHADOW申明深度图（直接用TEXTURE也行）：方向光阴影图集
//SAMPLER_CMP(sampler_DirectionalShadowAtlas);	//使用SAMPLER_CMP申明用于比较深度的采样器
#define SHADOW_SAMPLER sampler_linear_clamp_compare
SAMPLER_CMP(SHADOW_SAMPLER);  

CBUFFER_START(_CustomShadows)
	int _CascadeCount; //阴影级联数量
	float4 _CascadeCullingSpheres[MAX_CASCADE_COUNT];
	float4 _CascadeData[MAX_CASCADE_COUNT]; //x存储级联半径的倒数、y存储一个纹素大小
	//方向光阴影转换矩阵：从世界空间转换到阴影图集空间的矩阵
	float4x4 _DirectionalShadowMatrices[MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT * MAX_CASCADE_COUNT]; 
	float4 _ShadowAtlasSize; //阴影图集大小
	float4 _ShadowDistanceFade; //阴影距离渐变
CBUFFER_END

struct DirectionalShadowData //方向光阴影数据
{
	float strength;
	int tileIndex;
	float normalBias; //法线偏置
};

struct ShadowData
{
	int cascadeIndex; //像素选择哪个级联
	float cascadeBlend; //阴影级联混合
	float strength; //全局阴影强度：若物体超过了最后一级级联的范围，则控制其无阴影
};

/*
	* 根据深度设置全局阴影强度：
	* 渐变衰减函数：(1−d/m)/f 计算淡出，d是表面深度，m是最大阴影距离，f是淡出范围，表示为最大距离的分数
	* 计划让阴影在最大距离m附近渐变：用深度d / 最大距离m，深度越大得到的值越大（越接近1）；
	* 但阴影是为1时效果最强，所以进行取反：1 - d/m 得到深度越大数据越小；
	* 但此时整个视锥都会进入渐变，所以除以f：(1−d/m)/f ，f为渐变出现的范围，表示占据整个距离从最大距离往前的范围
	* 数学理解：x = 1 - d/m：得到1~0，深度越大数值越小，这时/f，f为一个分数，/分数 = *倍数，所以当x较小时倍数增长缓慢；
	* 使用1/m，1/f传递，是为了减少GPU计算除法，在GPU里直接 *1/m和1/f，就相当于/m和f
*/
float FadedShadowStrength(float distance, float scale, float fade)
{
	return saturate((1.0 - distance * scale) * fade); //使用saturate函数限制在0~1内
}

//获取阴影数据
ShadowData GetShadowData(Surface surfaceWS)
{
	ShadowData data;
	data.cascadeBlend = 1.0; //当前级联混合强度设为1
	data.strength = FadedShadowStrength(surfaceWS.depth, _ShadowDistanceFade.x, _ShadowDistanceFade.y); //根据深度设置全局阴影强度
	int i;
	for (i = 0; i < _CascadeCount; i++) //在级联阴影球体中循环
	{
		float4 sphere = _CascadeCullingSpheres[i]; //获取球体数据
		float distanceSqr = DistanceSquared(surfaceWS.position, sphere.xyz); //计算该像素到当前球体中心的距离的平方
		if(distanceSqr < sphere.w) //用距离平方和半径平方做比较，若小于半径平方，说明该像素位于当前循环的级联球体中
		{
			/*
				渐变函数：(1−d^2 /r^2)/1−(1−f)^2
				我们要制作最后一个级联的阴影渐变，因为计算级联的方法是使用深度的平方与半径的平方做比较
				所以不同于全局的阴影强度渐变，级联阴影渐变会变成非线性
				此时需要修改 f 的数据，用以让变化回归线性与全局同步
				语法解释：要使得 d=r(1-f) 时（深度 = 设定的渐变开始范围半径），渐变函数的结果为1，
				则需要让 f -> 1−(1−f)^2
			*/
			float fade = FadedShadowStrength(distanceSqr, _CascadeData[i].x, _ShadowDistanceFade.z);
			if(i == _CascadeCount - 1) //若当前处于最后一层级联
			{
				
				data.strength *= fade;
			}
			else
			{
				//在非最后级联使用阴影级联混合
				data.cascadeBlend = fade;
			}
			break;
		}
	}
	
	if(i == _CascadeCount)
	{
		data.strength = 0.0; //若物体超过了最后一级级联的范围，则控制其无阴影
	}
	#if defined(_CASCADE_BLEND_DITHER) //若当前不在最后一个级联且处于抖动混合模式 
	else if (data.cascadeBlend < surfaceWS.dither) //当混合小于抖动值时
	{
		i += 1; //跳到下一个级联，采样下一层的级联阴影贴图
	}
	#endif
	
	#if !defined(_CASCADE_BLEND_SOFT) //如果没有开启软混合
		data.cascadeBlend = 1.0; //将混合设置为1
	#endif
	
	data.cascadeIndex = i; //将当前级联的索引赋值
	return data;
}

//采样阴影图集
float SampleDirectionalShadowAtlas(float3 positionSTS)
{
	//采样阴影图集（阴影图集，采样器，阴影纹理空间位置）
	//因为是阴影贴图的采样，使用的是Compare模式，比较采样器会对比当前像素和阴影贴图中相同位置像素的深度
	//会返回0~1，0说明该像素被遮挡处于阴影中，1说明该函数没被遮挡
	return SAMPLE_TEXTURE2D_SHADOW(_DirectionalShadowAtlas, SHADOW_SAMPLER, positionSTS);
}

//方向光阴影过滤器
float FilterDirectionalShadow(float3 positionSTS)
{
#if defined(DIRECTIONAL_FILTER_SETUP)
	float weights[DIRECTIONAL_FILTER_SAMPLES];
	float2 positions[DIRECTIONAL_FILTER_SAMPLES];
	float4 size = _ShadowAtlasSize.yyxx; //纹素大小、图集大小
	//使用封装好的函数来减少采样次数，以提高性能(（纹素大小、图集大小），采样原始位置，每个样本的权重，每个样本的位置)
	DIRECTIONAL_FILTER_SETUP(size, positionSTS.xy, weights, positions); 
	float shadow = 0;
	for(int i = 0; i < DIRECTIONAL_FILTER_SAMPLES; i++)
	{
		//利用周围的像素的结果结合到当前片元的深度得到平均值
		shadow += weights[i] * SampleDirectionalShadowAtlas(float3(positions[i].xy, positionSTS.z));
	}
	return shadow;
#else
	return SampleDirectionalShadowAtlas(positionSTS);
#endif
}

//获取方向光阴影衰减（无阴影1~完全阴影0）
float GetDirectionalShadowAttenuation(DirectionalShadowData directional, ShadowData global, Surface surfaceWS)
{
	#if !defined(_RECEIVE_SHADOWS) //如果没有定义接受阴影
		return 1.0;
	#endif
	
	if (directional.strength <= 0.0)
	{
		return 1.0;
	}
	
	float3 normalBias = surfaceWS.normal * (directional.normalBias * _CascadeData[global.cascadeIndex].y); //法线偏置的向量
	
	//将世界空间中的像素位置转换到阴影贴图空间
	float3 positionSTS = mul(_DirectionalShadowMatrices[directional.tileIndex], float4(surfaceWS.position + normalBias, 1.0)).xyz;
	float shadow = FilterDirectionalShadow(positionSTS); //使用帐篷滤波器来计算阴影强度
	
	if(global.cascadeBlend < 1.0) //当前级联混合强度小于1说明当前处于级联交界地带，执行混合
	{
		//重新计算法线偏置：不同级联有不同的纹素大小，所以需要计算下一个级联的法线偏置
		normalBias = surfaceWS.normal * (directional.normalBias * _CascadeData[global.cascadeIndex + 1].y);
		//重新计算阴影贴图空间中的位置
		positionSTS = mul(
			_DirectionalShadowMatrices[directional.tileIndex + 1], float4(surfaceWS.position + normalBias, 1.0)
		).xyz;
		//重新计算帐篷滤波器的阴影结果并与上一个级联结果做线性插值
		shadow = lerp(FilterDirectionalShadow(positionSTS), shadow, global.cascadeBlend);
	}
	return lerp(1.0, shadow, directional.strength); //利用strength控制阴影的透明度；当strength为0时结果为1，完全没有阴影
}

#endif