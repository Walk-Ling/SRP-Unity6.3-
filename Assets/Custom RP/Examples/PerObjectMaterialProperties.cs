using UnityEngine;

[DisallowMultipleComponent] //禁止一个GameObject拥有多个该组件
public class PerObjectMaterialProperties : MonoBehaviour
{
	//将着色器属性名 "_BaseColor" 转换成整数 ID
	static int
		baseColorId = Shader.PropertyToID("_BaseColor"), //_BaseColor属性名固定，哈希后的ID也固定，所以使用static，避免重复运算
		cutoffId = Shader.PropertyToID("_Cutoff"),
		metallicId = Shader.PropertyToID("_Metallic"),
		smoothnessId = Shader.PropertyToID("_Smoothness");

	[SerializeField] //序列化字段：让私有字段在 Inspector 面板中可见、可编辑
	Color baseColor = Color.white;

	[SerializeField, Range(0f, 1f)] //序列化一个0~1的滑块
	float cutoff = 0.5f, metallic = 0f, smoothness = 0.5f;

	static MaterialPropertyBlock block; //用来设置材质属性

	//在组件加载或更改时自动被调用
	void OnValidate()
	{
		if(block == null)
		{
			block = new MaterialPropertyBlock();
		}

		baseColor = new Vector4(Random.value, Random.value, Random.value, Random.value < 0.25f ? 0f : 1f); //随机颜色，25%的概率为透明物体

		//获取该物体中的Renderer组件
		Renderer renderer = GetComponent<Renderer>();

		if(renderer != null)
		{
			//sharedMaterial直接获取材质的本体
			//此处不可获取material属性，因为每次调用就会创建一个实例，从而导致内存泄漏
			Material sharedMat = renderer.sharedMaterial;

			//如果物体是不透明物体则Alpha值为1
			if(sharedMat != null && sharedMat.renderQueue < 2450)
			{
				baseColor.a = 1f;
			}
		}

		block.SetColor(baseColorId, baseColor); //设置颜色函数，传入属性ID和颜色值
		block.SetFloat(cutoffId, cutoff); //设置该物体的透明度裁剪数值
		block.SetFloat(metallicId, metallic);	//设置物体的金属度
		block.SetFloat(smoothnessId, smoothness);	//设置物体粗糙度
		//将包含颜色修改的block对象绑定在此组件上
		//使用Block而不直接修改颜色：不会克隆材质实例，不产生额外的大量资源消耗；但会破坏SRP批处理的运行
		renderer.SetPropertyBlock(block);
	}

	private void Awake()
	{
		OnValidate(); //使其在构建物体时就调用
	}
}
