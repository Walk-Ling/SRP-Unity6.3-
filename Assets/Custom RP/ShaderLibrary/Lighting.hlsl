#ifndef CUSTOM_LIGHTING_INCLUDED
#define CUSTOM_LIGHTING_INCLUDED

//入射光
float3 IncomingLight(Surface surface, Light light)
{
	//颜色 = 表面法线·入射光源方向 * 投影颜色 * 灯光颜色
	return saturate(dot(surface.normal, light.direction) * light.attenuation) * light.color;
}

//获取表面光照的函数
float3 GetLighting(Surface surface, BRDF brdf, Light light)
{
	return IncomingLight(surface, light) * DirectBRDF(surface, brdf, light); //表面反射的颜色 = 入射光 * BRDF颜色
}

//获取表面光照的函数
float3 GetLighting(Surface surfaceWS, BRDF brdf)
{
	ShadowData shadowData = GetShadowData(surfaceWS); //获取阴影数据
	//使用循环来将光照与物体得到的颜色叠加
	//现代图形渲染对循环和逻辑运算的兼容性提高，可以适当使用
	float3 color = 0.0;
	for (int i = 0; i < GetDirectionalLightCount(); i++)
	{
		Light light = GetDirectionalLight(i, surfaceWS, shadowData);
		color += GetLighting(surfaceWS, brdf, light);
	}
	return color;
}

#endif