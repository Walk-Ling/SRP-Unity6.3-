using Unity.Collections;	//提供NativeArray数据结构，用于创建特殊属性的数组
using UnityEngine;
using UnityEngine.Rendering;

//将灯光数据发送到GPU
public class Lighting
{
	//创建CommandBuffer：用于存储渲染命令的对象，可以在不同阶段执行这些命令，现代Unity使用CommandBuffer来管理渲染命令
	//大部分自定义数据以及渲染命令都由CommandBuffer传入GPU
	const string bufferName = "Lighting";
	CommandBuffer buffer = new CommandBuffer { name = bufferName };

	const int maxDirLightCount = 4; //场景中有向光源的最大数量

	//跟踪两个着色器属性的标识符
	static int
		//dirLightColorId = Shader.PropertyToID("_DirectionalLightColor"),
		//dirLightDirectionId = Shader.PropertyToID("_DirectionalLightDirection");
		dirLightCountId = Shader.PropertyToID("_DirectionalLightCount"),
		dirLightColorsId = Shader.PropertyToID("_DirectionalLightColors"),
		dirLightDirectionsId = Shader.PropertyToID("_DirectionalLightDirections"),
		dirLightShadowDataId = Shader.PropertyToID("_DirectionalLightShadowData"); //存储投射阴影的灯光的阴影数据

	//GPU的常用传送数据类型为Vector4，所以一般使用Vector4类型来传输数据
	static Vector4[]
		dirLightColors = new Vector4[maxDirLightCount],
		dirLightDirections = new Vector4[maxDirLightCount],
		dirLightShadowData = new Vector4[maxDirLightCount];

	CullingResults cullingResults;  //当 Unity 执行剔除操作时，会将结果存储在 CullingResults 结构中

	Shadows shadows = new Shadows(); //阴影系统

	//设置属性
	//ScriptableRenderContext 是一个类，作为渲染流水线中的自定义 C# 代码与 Unity 底层图形代码之间的接口，使用 ScriptableRenderContext API 来调度和执行渲染命令
	public void Setup(ScriptableRenderContext context, CullingResults cullingResults, ShadowSettings shadowSettings)
	{
		this.cullingResults = cullingResults;
		buffer.BeginSample(bufferName); //开始一个新的采样，标记渲染命令的开始，方便调试和性能分析，在Profiler中查看
		shadows.Setup(context, cullingResults, shadowSettings); //设置阴影
		SetupLights();  //设置光源
		shadows.Render();
		buffer.EndSample(bufferName); //结束采样，标记渲染命令的结束

		context.ExecuteCommandBuffer(buffer); //执行命令缓冲区
		buffer.Clear(); //清理buffer，准备下一帧的渲染命令
	}

	//定义SetupLights函数，用以确定会对画面剔除后画面影响的光源
	void SetupLights()
	{
		//将剔除结果中的可见光源存入数组
		NativeArray<VisibleLight> visibleLights = cullingResults.visibleLights;

		//获取前max个数量的方向光数据
		int dirLightCount = 0;
		for(int i = 0; i < visibleLights.Length; i++)
		{
			VisibleLight visibleLight = visibleLights[i];
			if(visibleLight.lightType == LightType.Directional)
			{
				SetupDirectionalLight(dirLightCount++, ref visibleLight);
				if (dirLightCount >= maxDirLightCount)
				{
					break;
				}
			}
		}

		//将数据传给GPU
		buffer.SetGlobalInt(dirLightCountId, dirLightCount);	//传入光源数量
		buffer.SetGlobalVectorArray(dirLightColorsId, dirLightColors);	//传入光源颜色
		buffer.SetGlobalVectorArray(dirLightDirectionsId, dirLightDirections);  //传入光源方向
		buffer.SetGlobalVectorArray(dirLightShadowDataId, dirLightShadowData); //传入光源阴影数据
	}

	//清除渲染纹理和缓冲区
	public void Cleanup()
	{
		shadows.Cleanup();
	}

	//获取方向光数据：使用引用参数来减少创建复制体的开销
	void SetupDirectionalLight(int index, ref VisibleLight visibleLight)
	{
		dirLightColors[index] = visibleLight.finalColor;
		/*
		 * 语法理解：
		 *		我们需要该方向光的方向，Unity默认方向光自身空间的前向即z方向为他的光方向
		 *		localToWorldMatrix——将方向光从自身空间变换到世界空间
		 *		GetColumn(2)——获取第三列数据，Unity默认进行列存储：
		 *			0——第一列：存储x方向分量 == right分量
		 *			1——第二列：存储y方向分量 == up分量
		 *			2——第三列：存储z方向分量 == forward分量
		 *			3——第四列：存储位置，方向光不在意位置，所以没用
		 *		"-"——取反，方便后续点乘运算
		 */
		dirLightDirections[index] = -visibleLight.localToWorldMatrix.GetColumn(2);
		dirLightShadowData[index] = shadows.ReserveDirectionalShadows(visibleLight.light, index); //预留阴影贴图空间
	}
}
