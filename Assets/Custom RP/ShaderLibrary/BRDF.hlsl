#ifndef CUSTOM_BRDF_INCLUDED
#define CUSTOM_BRDF_INCLUDED

#define MIN_REFLECTIVITY 0.04	//非金属的最小反射率

//BRDF双向反射分布函数结构体：用于定义物体的材质
struct BRDF
{
	float3 diffuse;	//漫反射
	float3 specular;	//镜面反射
	float roughness;	//粗糙度
};

//最大反射率
float OneMinusReflectivity(float metallic)
{
	float range = 1.0 - MIN_REFLECTIVITY;	//漫反射的最大能量
	return range - metallic * range; //剩余漫反射能量 = 漫反射的最大能量（0.96）- 金属度所占用的能量（metallic * range）
}

//获取材质表面的BRDF
BRDF GetBRDF(Surface surface, bool applyAlphaToDiffuse = false)
{
	BRDF brdf;
	
	float oneMinusReflectivity = OneMinusReflectivity(surface.metallic); //漫反射强度：物体越接近金属，镜面反射就越强，漫反射就越弱
	float perceptualRoughness = 1 - surface.smoothness; //感知粗糙度 = 1 - 光滑度；
	//因为感知粗糙度0~1的变化是非线性的，所以需要将其进行平方转化为物体粗糙度，方便进行视觉调整和物理计算
	brdf.roughness = perceptualRoughness * perceptualRoughness; //物理粗糙度 = 感知粗糙度的平方
	
	brdf.diffuse = surface.color * oneMinusReflectivity;	//漫反射 = 颜色 * 漫反射强度
	if (applyAlphaToDiffuse) //控制透明物体的反射颜色是否会受到透明度的影响，默认为false
	{
		brdf.diffuse *= surface.alpha; //乘上透明度，保证半透明物体的漫反射颜色也会变淡
	}
	brdf.specular = lerp(MIN_REFLECTIVITY, surface.color, surface.metallic);	//高光颜色 = 线性插值（最低反射率，物体自身颜色，物体金属度）
	brdf.specular = 1.0;
	return brdf;
}

//计算高光强度：GGX模型，基于微表面理论的物理基础，考虑了表面微观结构对光线反射的影响
//计算公式：r^2 / d^2 * max(0.1,(L⋅H)^2) * n, d = (N⋅H)^2 * (r^2 − 1) + 1.0001
float SpecularStrength(Surface surface, BRDF brdf, Light light)
{
	//SafeNormalize：当输入向量长度为0时，返回一个默认的单位向量，避免除零错误。
	float3 h = SafeNormalize(light.direction + surface.viewDirection); //半程向量 = 入射光方向 + 视角方向
	float nh2 = Square(saturate(dot(surface.normal, h))); //计算半程向量与法线的夹角余弦值
	float lh2 = Square(saturate(dot(light.direction, h))); //半程向量与入射方向的点乘
	float r2 = Square(brdf.roughness); //分子 r2：粗糙度的平方，控制高光峰值。
	/*
		GGX模型的高光强度计算公式为NDF（法线分布函数）：D = r^2 / (pi * ((n.h)^2 * (r^2 - 1) + 1)^2))
		这里的 d2 其实是分母的平方（去掉了 π），后面会和分子 r2 一起组成完整的 D 项。
		1.00001 是为了防止 r2 = 0 且 nh2 = 1 时，分母变成 0，避免除零错误。
	 */
	float d2 = Square(nh2 * (r2 - 1.0) + 1.00001); //d²：法线分布项，决定了高光形状。
	float normalization = brdf.roughness * 4.0 + 2.0; //归一化，这是 GGX 分布的一个经验归一化因子 4r + 2，保证不同粗糙度下高光能量大致守恒。
	//max(0.1, lh2)：自遮蔽项模拟，避免入射角在掠射角时高光强度过高
	return r2 / (d2 * max(0.1, lh2) * normalization); //高光强度 = 粗糙度的平方 / （分母 * 自遮蔽项模拟 * 归一化项）
}

//计算表面反射的颜色：将高光强度与高光颜色和漫反射颜色相结合，得到最终的表面反射颜色
float3 DirectBRDF(Surface surface, BRDF brdf, Light light)
{
	return SpecularStrength(surface, brdf, light) * brdf.specular + brdf.diffuse;
}

#endif