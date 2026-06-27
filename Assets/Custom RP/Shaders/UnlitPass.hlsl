//如果宏已经被定义，则跳过内部代码
#ifndef CUSTOM_UNLIT_PASS_INCLUDE	//#ifndef = "if not defined"
#define CUSTOM_UNLIT_PASS_INCLUDE	//include保护：定义了一个宏，用于避免重复包含

#include "../ShaderLibrary/Common.hlsl"

//纹理和采样器状态都是独立着色器资源。不能按实例提供，必须在全局范围内声明
TEXTURE2D(_BaseMap); //定义纹理常量
SAMPLER(sampler_BaseMap); //定义纹理采样器

/*
	将属性放入常量缓冲区cbuffer，方便进行SRP批处理，以减少DrawCall的准备时间；SRP 批处理并不减少DrawCall
	并非每个API都是存入cbuffer中，因此使用 Core RP Library 中包含的宏来构建
	注意：常量缓冲区很小，只推荐存储如颜色、浮点数等小数据，较大的数据如纹理等不适合存放在常量缓冲区中
	适用于处理场景种类繁多，许多物体共享同一个Shader，但各有不同的材质；降低每次DrawCall的调用速度
*/
//CBUFFER_START(UnityPerMaterial)
//	float4 _BaseColor;
//CBUFFER_END

//二者只能使用其一，SRP批处理优先级比GPU实例化高

/*
	GPU实例化：
		UNITY_VERTEX_INPUT_INSTANCE_ID 在结构体中声明实例编号字段，
		UNITY_SETUP_INSTANCE_ID 激活它，
		UNITY_TRANSFER_INSTANCE_ID 在顶点和片元之间传递它，
		UNITY_ACCESS_INSTANCED_PROP 用它取出当前物体的专属数据。
	注意：GPU 实例化仅适用于共享相同材质的对象
	适用于处理反复绘制完全一样的小物体；降低DrawCall数量
*/
//#pragma multi_compile_instancing //关键字会自动检测状态，满足SRP批处理时会自动将以下常量缓冲区转换为符合SRP批处理的缓冲区

//声明一个逐实例的常量缓冲区，存入其中的属性可以使用特定的宏根据不同的实例进行不同的修改，不支持类似贴图的属性存入
//当GPU实例化不生效时，这些宏会退化至常量缓冲区
UNITY_INSTANCING_BUFFER_START(UnityPerMaterial) 
	UNITY_DEFINE_INSTANCED_PROP(float4, _BaseMap_ST) //用于控制贴图偏移和缩放的实例属性
	UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)	//在这个缓冲区中声明一个属性 _BaseColor，类型为 float4。在 Instancing 模式下，它实际是一个数组，每个实例可以拥有不同的值。
	UNITY_DEFINE_INSTANCED_PROP(float, _Cutoff) //透明度裁剪数值
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

struct Attributes //属性（顶点着色器输入结构体）
{
	float3 positionOS : POSITION;
	float2 baseUV : TEXCOORD0;
	UNITY_VERTEX_INPUT_INSTANCE_ID	//自动为当前的物体赋予一个实例化的索引ID
};

struct Varyings //变化量（片元着色器输入结构体）
{
	float4 positionCS : SV_POSITION;
	float2 baseUV : VAR_BASE_UV; //自定义语义，为了满足语法，无特殊意义
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

//顶点着色器
Varyings UnlitPassVertex(Attributes input)
{
	Varyings output;
	UNITY_SETUP_INSTANCE_ID(input); //根据input里的ID设置为当前实例的ID
	UNITY_TRANSFER_INSTANCE_ID(input, output); //把 ID 复制到 output 结构体中
	
	float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
	output.positionCS = TransformWorldToHClip(positionWS);
	
	//利用宏来正确处理当前shader实例
	float4 baseST = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseMap_ST);
	output.baseUV = input.baseUV * baseST.xy + baseST.zw; //xy分量存储缩放，zw分量存储偏移
	return output;
}

//片元着色器
float4 UnlitPassFragment(Varyings input) : SV_TARGET
{	
	UNITY_SETUP_INSTANCE_ID(input);
	//使用基础纹理和采样器，在给定的UV坐标处采样，得到颜色值
	float4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.baseUV);
	float4 baseColor = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseColor);

	#if defined(_CLIPPING)
		// ---- Alpha 裁剪模式 ----
		// 颜色直接相乘，不需要预乘 Alpha，裁剪只看 base.a 是否超过阈值
		float4 base = baseMap * baseColor;
	
		//当Cutoff大于等于1时返回1，否则返回0，*1000后要么是一个大正数，要么为0
		float offset = step(1.0f, UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Cutoff)) * 1000.0f;
	
		//当Cutoff传入的数值因为浮点数的原因稍微大于1时，offset就不为0，从而将这个多出来的浮点数消去，使得正确裁剪像素
		clip(base.a - UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Cutoff) - offset); //clip：当内部当前数值小于0，则丢弃该像素
	#else
		// ---- Alpha 混合模式 ----
		// 贴图作为透明度遮罩，预乘 Alpha 让边缘柔和过渡
		float4 base = baseMap.a * baseColor;
	#endif

	return base;
}

#endif