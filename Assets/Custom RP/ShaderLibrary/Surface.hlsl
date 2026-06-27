#ifndef CUSTOM_SURFACE_INCLUDED
#define CUSTOM_SURFACE_INCLUDED

//表面数据：方便对光照表面的数据进行管理
struct Surface
{
	float3 position;
	float3 normal;
	float3 viewDirection; //视角方向——从表面指向相机
	float depth;
	float3 color;
	float alpha;
	float metallic;	//金属度
	float smoothness;	//粗糙度
	float dither;	//阴影级联混合抖动幅度
};

#endif