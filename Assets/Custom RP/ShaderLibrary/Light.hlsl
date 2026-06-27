#ifndef CUSTOM_LIGHT_INCLUDED
#define CUSTOM_LIGHT_INCLUDED

#define MAX_DIRECTIONAL_LIGHT_COUNT 4	//定义最大光源数量

//自定义常量缓冲区用于存储光照属性
CBUFFER_START(_CustomLight)
	//float3 _DirectionalLightColor;		//获取世界灯光的颜色
	//float3 _DirectionalLightDirection;	//获取世界灯光的方向（物体到灯光）
	int _DirectionalLightCount;	//方向光数量
	float4 _DirectionalLightColors[MAX_DIRECTIONAL_LIGHT_COUNT];	//方向光颜色
	float4 _DirectionalLightDirections[MAX_DIRECTIONAL_LIGHT_COUNT];	//方向光方向
	float4 _DirectionalLightShadowData[MAX_DIRECTIONAL_LIGHT_COUNT]; //方向光阴影数据
CBUFFER_END

//定义光照
struct Light
{
	float3 color;
	float3 direction;
	float attenuation; //阴影衰减
};

//获取方向光数量
int GetDirectionalLightCount()
{
	return _DirectionalLightCount;
}

//获取方向光阴影数据
DirectionalShadowData GetDirectionalShadowData(int lightIndex, ShadowData shadowData)
{
	DirectionalShadowData data;
	//提取当前灯光的阴影强度，并 * 全局阴影强度
	data.strength = _DirectionalLightShadowData[lightIndex].x * shadowData.strength;
	//提取当前灯光的阴影第一个级联瓦片索引，并添加当前级联阴影偏移
	data.tileIndex = _DirectionalLightShadowData[lightIndex].y + shadowData.cascadeIndex;
	data.normalBias = _DirectionalLightShadowData[lightIndex].z; //法线偏置
	return data;
}

//灯光方向一般都是获取从物体到灯光的方向
Light GetDirectionalLight(int index, Surface surfaceWS, ShadowData shadowData)
{
	Light light;
	light.color = _DirectionalLightColors[index].rgb;
	light.direction = _DirectionalLightDirections[index].xyz;
	DirectionalShadowData dirShadowData = GetDirectionalShadowData(index, shadowData); //获取灯光阴影数据
	light.attenuation = GetDirectionalShadowAttenuation(dirShadowData, shadowData, surfaceWS);	//获取灯光阴影衰减
	//light.attenuation = shadowData.cascadeIndex * 0.25;	用于显式查看使用了那个级联
	return light;
}

#endif