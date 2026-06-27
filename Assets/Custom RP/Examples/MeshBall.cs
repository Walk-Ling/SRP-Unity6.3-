using UnityEngine;

public class MeshBall : MonoBehaviour
{
    static int 
        baseColorID = Shader.PropertyToID("_BaseColor"),
        metallicID = Shader.PropertyToID("_Metallic"),
        smoothnessID = Shader.PropertyToID("_Smoothness"),
        cutoffID = Shader.PropertyToID("_Cutoff"); //不同的透明度裁剪

	[SerializeField]
    Mesh mesh = default;

    [SerializeField]
    Material material = default;

    //GPU实例化单次调用最多1023次
    Matrix4x4[] matrices = new Matrix4x4[1023];
    Vector4[] baseColors = new Vector4[1023];
    float[]
        metalic = new float[1023],
        smoothness = new float[1023],
        cutoffs = new float[1023];

	MaterialPropertyBlock block; //用以传递颜色数据

	private void Awake()
	{
		//创建小球的信息
		for (int i = 0; i < matrices.Length; i++)
        {
			//Matrix4x4.TRS：将位置、旋转、缩放，打包成一个GPU能直接读取的4x4矩阵
			matrices[i] = Matrix4x4.TRS(
                Random.insideUnitSphere * 10f, //位置：半径为10的球体内任取一个点
                Quaternion.Euler(Random.value * 360f, Random.value * 360f, Random.value * 360f), //旋转
                Vector3.one * Random.Range(0.5f, 1.5f) //缩放
            );
			baseColors[i] = new Vector4(Random.value, Random.value, Random.value, Random.Range(0.5f, 1f)); //随机颜色，不透明
            metalic[i] = Random.value < 0.25f ? 1f : 0f; //25%的概率为金属材质
            smoothness[i] = Random.Range(0.05f, 0.95f); //随机粗糙度
            cutoffs[i] = Random.Range(0.2f, 0.5f); //随机裁剪值
        }
	}

	private void Update()
	{
		if(block == null)
        {
            //创建用于替换的Block，并修改该Block的颜色属性
            block = new MaterialPropertyBlock();
            //传递修改参数的数组
            block.SetVectorArray(baseColorID, baseColors);
            block.SetFloatArray(metallicID, metalic);
            block.SetFloatArray(smoothnessID, smoothness);
            block.SetFloatArray(cutoffID, cutoffs);
        }
        //创建GPU实例，该函数单词调用最多1023次
        //属性：mesh, sub_mesh, material, 矩阵数组, 元素数量, block
        Graphics.DrawMeshInstanced(mesh, 0, material, matrices, 1023, block);
	}
}
