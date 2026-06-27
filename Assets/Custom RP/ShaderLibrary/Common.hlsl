#ifndef CUSTOM_COMMON_INCLUDED
#define CUSTOM_COMMON_INCLUDED

//Common是Unity为所有SRP提供的一个公共函数库，包含了各种常用的函数和宏定义，以及跨平台的输入变量
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "UnityInput.hlsl"

//float3 TransformObjectToWorld(float3 positionOS)
//{
//	return mul(unity_ObjectToWorld, float4(positionOS, 1.0)).xyz;
//}

//float4 TransformWorldToHClip(float3 positionWS)
//{
//	return mul(unity_MatrixVP, float4(positionWS, 1.0));
//}

//SpaceTransforms文件内部的各种变换相关函数，都采用以下宏定义的矩阵变量，我们定义宏相当于告诉函数这些变量是什么，从而让函数正确运行
#define UNITY_MATRIX_M unity_ObjectToWorld			//物体->世界
#define UNITY_MATRIX_I_M unity_WorldToObject		//世界->物体
#define UNITY_MATRIX_V unity_MatrixV				//世界->相机
#define UNITY_MATRIX_I_V unity_MatrixInvV			//相机->世界
#define UNITY_MATRIX_VP unity_MatrixVP				//世界->裁剪空间
#define UNITY_PREV_MATRIX_M unity_prev_MatrixM		//上一帧的物体->世界
#define UNITY_PREV_MATRIX_I_M unity_prev_MatrixIM	//上一帧的世界->物体
#define UNITY_MATRIX_P glstate_matrix_projection	//投影矩阵

//包含GPU实例化方法库，需在定义了矩阵后和变换函数库之前包含
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl" //用以重新定义宏，以访问实例化数组
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl" //变换函数核心库

float Square(float v)
{
	return v * v;
}

//两点之间的距离平方
float DistanceSquared(float3 pA, float3 pB)
{
	return dot(pA - pB, pA - pB);	//向量自身的平方 = 模长的平方（模长 = 开平方（x^2 + y^2 + z^2））
}

#endif