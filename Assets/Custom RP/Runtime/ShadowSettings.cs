using UnityEngine;

[System.Serializable]   // 标记这个类可以被 Unity 序列化，从而在 Inspector 中显示并保存其字段数据
public class ShadowSettings
{
	[Min(0.001f)] //限制这个字段的最小值为0.001，因为后续制作线性变淡时需要用作除数
	public float maxDistance = 100f; //拥有阴影的最大距离，超出这个距离的物体不在投射阴影

	[Range(0.001f, 1f)]
	public float distanceFade = 0.1f; //距离逐渐消失范围

	/// <summary>
	/// PCF阴影滤波核：用于实现软阴影（默认2*2）
	/// </summary>
	public enum FilterMode
	{ 
		PCF2x2, PCF3x3, PCF5x5, PCF7x7
	}

	//阴影贴图的大小，贴图越大，阴影质量越好，性能开销越大；最好是2的幂次方，且不小于256
	public enum MapSize
	{
		_256 = 256,
		_512 = 512,
		_1024 = 1024,
		_2048 = 2048,
		_4096 = 4096,
		_8192 = 8192
	}
	
	[System.Serializable]
	public struct Directional
	{
		public MapSize atlasSize; //阴影贴图的大小

		public FilterMode filter; //阴影滤波核

		[Range(1, 4)]
		public int cascadeCount; //阴影级联数量

		[Range(0f, 1f)]
		public float cascadeRatio1, cascadeRatio2, cascadeRatio3; //每个级联占据的距离比例

		//ComputeDirectionalShadowMatricesAndCullingPrimitives 函数需要Vector类型来传递数据
		public Vector3 CascadeRatios => new Vector3(cascadeRatio1, cascadeRatio2, cascadeRatio3);

		[Range(0.001f, 1f)]
		public float cascadeFade; //控制使用最后一个级联的阴影减弱

		/// <summary>
		/// 级联混合模式：硬、软、抖动
		/// </summary>
		public enum CascadeBlendMode 
		{
			Hard, Sort, Dither
		}

		public CascadeBlendMode cascadeBlend;
	}

	public Directional directional = new Directional
	{
		atlasSize = MapSize._1024,
		filter = FilterMode.PCF2x2,
		cascadeCount = 4,   //四层级联
		cascadeRatio1 = 0.1f,   //占据原距离的前0.1
		cascadeRatio2 = 0.25f,  //占据原距离的前0.25
		cascadeRatio3 = 0.5f,   //占据原距离的前0.5
								//第四个级联包含所有距离，所以不需要
		cascadeFade = 0.1f, //级联衰减
		cascadeBlend = Directional.CascadeBlendMode.Hard	//设置级联混合模式
	};
}
