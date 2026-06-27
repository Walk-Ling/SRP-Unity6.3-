#ifndef CUSTOM_UNITY_INPUT_INCLUDED
#define CUSTOM_UNITY_INPUT_INCLUDED

//变换组的数据被存放在UnityPreDraw缓冲区中，unity通过索引来访问他们，所以内部的属性要么全有要么全没有，但顺序并不重要
//以下矩阵每个物体数据不一样
CBUFFER_START(UnityPerDraw)
	//Unity内置的输入变量，包含了从CPU传递到GPU的各种数据，如变换矩阵、时间、光照信息等；
	//只要Camera设置好了属性SetupCameraProperties，就能够正确使用，而不需要担心他们的包含和更新
	float4x4 unity_ObjectToWorld;
	float4x4 unity_WorldToObject;
	float4 unity_LODFade; //LOD 过渡因子
	//real：是Common中定义的一个类型，根据平台不同可能是float、double、half等；
	real4 unity_WorldTransformParams; //世界变换参数：用w分量存储了一个标志，指示是否需要进行翻转操作，通常在处理物体是否进行翻转时使用，来调节变换矩阵的计算方式
CBUFFER_END

//以下矩阵每个物体数据都一样
//这些矩阵是引擎根据摄像机自动设置的，直接声明为全局变量即可
float4x4 unity_MatrixVP;
float4x4 unity_MatrixV;
float4x4 unity_MatrixInvV;
float4x4 unity_prev_MatrixM;
float4x4 unity_prev_MatrixIM;
float4x4 glstate_matrix_projection;

float3 _WorldSpaceCameraPos; //世界空间相机位置
#endif