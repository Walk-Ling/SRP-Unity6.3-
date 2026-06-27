Shader "Custom RP/Lit"
{
	Properties
	{
		_BaseMap("Texture", 2D) = "white"{}
		_BaseColor("Base Color", Color) = (0.5, 0.5, 0.5, 1)
		_Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5 //透明度裁剪
		[Toggle(_CLIPPING)] _Clipping("Alpha Clipping", Float) = 0 //使用开关控制裁剪

		//在材质的 Inspector 面板上，直接通过下拉菜单控制混合模式（属性）
		[Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Src Blend", Float) = 1 //源混合：当前绘制的内容
		[Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Dst Blend", Float) = 0 //目标混合：之前绘制的内容，以及最终颜色的缓冲区

		//使用列表选项添加ZWrite开关选项
		[Enum(Off, 0, On, 1)] _ZWrite("Z Write", Float) = 1

		//PBR
		_Metallic("Metallic 金属度", Range(0,1)) = 0
		_Smoothness("Smoothness 光滑度", Range(0,1)) = 0.5
		
		[Toggle(_PREMULTIPLY_ALPHA)] _PremulAlpha("Premultyply Alpha 预乘Alpha", Float) = 0
		
		//阴影模式关键字：KeywordEnum会创建一个下拉菜单用来供用户选择关键字，选用的关键字将被自动开启，其他被自动禁用
		[KeywordEnum(On, Clip, Dither, Off)] _Shadows ("Shadows", Float) = 0
		
		[Toggle(_RECEIVE_SHADOWS)] _ReceiveShadows ("Receive Shadows", Float) = 1 //使用开关来确定是否接受阴影
	}

	SubShader
	{
		Pass
		{
			Name "CustomLitPass"

			Tags
			{
				"LightMode" = "CustomLit" //自定义光照模式
			}

			//标准透明材质混合模式为_SrcBlend：SrcColor；_DstBlend：OneMinusSrcAlpha；
			//表示当前透明材质使用透明材质自身的 Alpha 作为比例进行混合，颜色缓冲区使用 1-Alpha 数值进行混合
			Blend [_SrcBlend] [_DstBlend]
			ZWrite [_ZWrite]

			HLSLPROGRAM
			//仅接受Shader Model3.5及以上的版本，旧版本不再兼容
			#pragma target 3.5

			#pragma shader_feature _CLIPPING //透明度裁剪开关，使用shader_feature在编译时就生成变体
			#pragma shader_feature _RECEIVE_SHADOWS //使用开关来确定是否接受阴影
			#pragma shader_feature _PREMULTIPLY_ALPHA //预乘Alpha开关
			//添加关键字，用于生成着色器变体，”_“表示一个占位符，用于处理其后所有关键字都不生效的情况
			#pragma multi_compile _ _DIRECTIONAL_PCF3 _DIRECTIONAL_PCF5 _DIRECTIONAL_PCF7
			#pragma multi_compile _ _CASCADE_BLEND_SORT _CASCADE_BLEND_DITHER
			#pragma multi_compile_instancing //GPU 实例需要通过数组提供数据，编译GPU实例化的指令开关，生成一个可以开启GPU实例化的着色器变体，细节面板生成开关

			#pragma vertex LitPassVertex
			#pragma fragment LitPassFragment
			#include "LitPass.hlsl"

			ENDHLSL
		}

		Pass
		{
			Name "ShadowCaster"

			Tags
			{
				"LightMode" = "ShadowCaster" //阴影投射Pass，使该材质能够投射阴影
			}

			ColorMask 0 //只写入深度，所以不写入颜色
			ZWrite On

			HLSLPROGRAM
			#pragma target 3.5
			//#pragma shader_feature _CLIPPING //使得投射的阴影同样支持透明度裁剪
			#pragma shader_feature _ _SHADOWS_CLIP _SHADOWS_DITHER //无关键字的开/关，透明度裁剪，抖动阴影
			#pragma multi_compile_instancing //支持GPU实例化
			#pragma vertex ShadowCasterPassVertex
			#pragma fragment ShadowCasterPassFragment
			#include "ShadowCasterPass.hlsl"
			ENDHLSL
		}
	}

	CustomEditor "CustomShaderGUI"	//自定义材质细节面板，使用CustomEditor来让Unity显示自定义的面板
}