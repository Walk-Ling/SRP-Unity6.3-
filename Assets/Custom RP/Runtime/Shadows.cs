using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using static UnityEditor.U2D.ScriptablePacker;

//将灯光数据发送到GPU
public class Shadows
{

	//创建CommandBuffer：用于存储渲染命令的对象，可以在不同阶段执行这些命令，现代Unity使用CommandBuffer来管理渲染命令
	//大部分自定义数据以及渲染命令都由CommandBuffer传入GPU
	const string bufferName = "Shadows";
	CommandBuffer buffer = new CommandBuffer { name = bufferName };

	ScriptableRenderContext context; //上下文类，执行各种渲染命令的接口

	CullingResults cullingResults;  //当 Unity 执行剔除操作时，会将结果存储在 CullingResults 结构中

	ShadowSettings settings; //阴影设置

	int ShadowedDirectionalLightCount; //当前需要投射阴影的灯光数量

	const int
		maxShadowedDirectionalLightCount = 4, //最大阴影灯光数量
		maxCascades = 4; //最大级联数量

	static int
		dirShadowAtlasId = Shader.PropertyToID("_DirectionalShadowAtlas"), //利用纹理属性来存储阴影贴图
		dirShadowMatricesId = Shader.PropertyToID("_DirectionalShadowMatrices"), //存储每个需要投射阴影的灯光的阴影矩阵
		cascadeCountId = Shader.PropertyToID("_CascadeCount"), //级联数
		cascadeCullingSpheresId = Shader.PropertyToID("_CascadeCullingSpheres"), //级联剔除球体
		cascadeDataId = Shader.PropertyToID("_CascadeData"), //级联数据
		shadowAtlasSizeId = Shader.PropertyToID("_ShadowAtlasSize"), //阴影图集大小
		shadowDistanceFadeId = Shader.PropertyToID("_ShadowDistanceFade"); //阴影距离淡出

	//存储每个需要投射阴影的灯光的阴影矩阵，每个级联阴影需要自己的变换矩阵，所以需要*最大阴影级联数量
	static Matrix4x4[] dirShadowMatrices = new Matrix4x4[maxShadowedDirectionalLightCount * maxCascades];

	static Vector4[]
		cascadeCullingSpheres = new Vector4[maxCascades], //级联剔除球体数组：xyz记录位置，w记录半径
		cascadeData = new Vector4[maxCascades]; //记录级联数据

	/// <summary> 
	/// 滤波核着色器变体数组 
	/// </summary>
	static string[] directionalFilterKeywords = { "_DIRECTIONAL_PCF3", "_DIRECTIONAL_PCF5", "_DIRECTIONAL_PCF7" };

	///<summary>
	///需要投射阴影的灯光信息
	///</summary>
	struct ShadowedDirectionalLight 
	{
		public int visibleLightIndex; //需要投射阴影的灯光在可见灯光数组中的索引
		public float slopeScaleBias; //斜率缩放偏置
		public float nearPlaneOffset; //近平面偏移
	}

	//使用一个数组来存储所有需要绘制阴影的灯光数据，数组大小为最大阴影灯光数量
	ShadowedDirectionalLight[] ShadowedDirectionalLights = new ShadowedDirectionalLight[maxShadowedDirectionalLightCount];

	/// <summary>
	/// 级联混合关键字
	/// </summary>
	private static string[] cascadeBlendKeywords =
	{
		"_CASCADE_BLEND_SORT",	//软
		"_CASCADE_BLEND_DITHER"	//抖动
	};
	
	/// <summary>
	/// 设置属性
	/// </summary>
	/// <param name="context">可编程管线上下文</param>
	/// <param name="cullingResults">剔除结果</param>
	/// <param name="settings">阴影设置</param>
	/// <remarks>ScriptableRenderContext 是一个类，作为渲染流水线中的自定义 C# 代码与 Unity 底层图形代码之间的接口，使用 ScriptableRenderContext API 来调度和执行渲染命令</remarks>
	public void Setup(ScriptableRenderContext context, CullingResults cullingResults, ShadowSettings settings)
	{
		this.context = context;
		this.cullingResults = cullingResults;
		this.settings = settings;
		ShadowedDirectionalLightCount = 0; //每次设置时重置需要投射阴影的灯光数量
	}

	/// <summary>
	/// 预留阴影
	/// </summary>
	/// <param name="light">灯光</param>
	/// <param name="visibleLightIndex">可见光索引</param>
	/// <returns></returns>
	/// <remarks>在阴影图集（Shadow Atlas）中为该光源的阴影贴图预留空间，并保存之后渲染这些阴影所需的信息。</remarks>
	public Vector3 ReserveDirectionalShadows(Light light, int visibleLightIndex)
	{
		if (
			ShadowedDirectionalLightCount < maxShadowedDirectionalLightCount && //如果当前需要投射阴影的灯光数量小于最大值
			light.shadows != LightShadows.None && //当前灯光阴影类型不为None
			light.shadowStrength > 0f && //当前灯光阴影强度大于0
			/*
			 * cullingResults.GetShadowCasterBounds（）
			 * 在当前剔除范围内，获取该光源的阴影投射范围即投射阴影物体的包围盒；
			 * 函数返回布尔值，若该包围盒内的物体处于剔除范围内，则返回true，否则返回false；
			 * 不投射阴影的物体不在包围盒内
			 */
			cullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds b)
		)
		{
			//就利用计数来创建一个新的实例，并记录索引添加到数组中
			ShadowedDirectionalLights[ShadowedDirectionalLightCount] = new ShadowedDirectionalLight { 
				visibleLightIndex = visibleLightIndex, //可见光索引
				slopeScaleBias = light.shadowBias, //斜率偏置
				nearPlaneOffset = light.shadowNearPlane //光源近平面偏移
			};

			return new Vector3(
				light.shadowStrength, //x分量为当前灯光的阴影强度
				settings.directional.cascadeCount * ShadowedDirectionalLightCount++, //y分量为预留当前灯光的级联阴影的索引的开头（即四个级联的第一个级联索引）
				light.shadowNormalBias	//z：法线偏置
			);
		}

		return Vector3.zero;
	}

	/// <summary>
	/// 设置瓦片视口
	/// </summary>
	/// <param name="index">灯光索引</param>
	/// <param name="split">分割量</param>
	/// <param name="tileSize">阴影贴图在图集中的大小</param>
	/// <returns>偏移量</returns>
	Vector2 SetTileViewport(int index, int split, float tileSize)
	{
		//根据当前灯光索引和图集被分割的数量来计算瓦片在图集中的偏移位置
		//x = index % split, y = index / split
		Vector2 offset = new Vector2(index % split, index / split);

		//buffer.SetViewport（Rect）将当前的渲染目标的渲染位置和大小设置为一个矩形区域
		//Rect：一个矩形结构体，包含了矩形的左下角坐标（x,y）和矩形的宽度和高度（width,height）
		buffer.SetViewport(new Rect(offset.x * tileSize, offset.y * tileSize, tileSize, tileSize)); //设置当前瓦片的视口，即在图集中的位置和大小

		return offset;
	}

	/// <summary>
	/// 世界->图集矩阵
	/// </summary>
	/// <param name="m">光源矩阵</param>
	/// <param name="offset">偏移量</param>
	/// <param name="split">分割量</param>
	/// <returns>世界->图集矩阵</returns>
	/// <remarks>因为使用的是图集，所以单纯的阴影矩阵会发生采样错误，所以需要在对应到图集中的基础上考虑缩放以及偏移量</remarks>
	Matrix4x4 ConvertToAtlasMatrix(Matrix4x4 m, Vector2 offset, int split)
	{
		//判断是否使用了反转Z缓冲区（在一些图形API中，深度值的范围是反转的，即进1——远0，所以需要进行特殊处理）
		if (SystemInfo.usesReversedZBuffer) 
		{
			//如果使用了反转Z缓冲区，则需要将阴影矩阵的第三列（z轴）取反，以适应反转的深度范围
			m.m20 = -m.m20;
			m.m21 = -m.m21;
			m.m22 = -m.m22;
			m.m23 = -m.m23;
		}
		//0.5f和m.m30，m.m31，m.m32，m.m33是为了将深度从[-1, 1]范围转换到[0, 1]范围，模仿的 NDC -> UV空间 转换矩阵展开后的结果（将xy映射到uv中，将z映射到0~1）
		//附加偏移和缩放
		float scale = 1f / split;
		m.m00 = (0.5f * (m.m00 + m.m30) + offset.x * m.m30) * scale;
		m.m01 = (0.5f * (m.m01 + m.m31) + offset.x * m.m31) * scale;
		m.m02 = (0.5f * (m.m02 + m.m32) + offset.x * m.m32) * scale;
		m.m03 = (0.5f * (m.m03 + m.m33) + offset.x * m.m33) * scale;
		m.m10 = (0.5f * (m.m10 + m.m30) + offset.y * m.m30) * scale;
		m.m11 = (0.5f * (m.m11 + m.m31) + offset.y * m.m31) * scale;
		m.m12 = (0.5f * (m.m12 + m.m32) + offset.y * m.m32) * scale;
		m.m13 = (0.5f * (m.m13 + m.m33) + offset.y * m.m33) * scale;
		m.m20 = 0.5f * (m.m20 + m.m30);
		m.m21 = 0.5f * (m.m21 + m.m31);
		m.m22 = 0.5f * (m.m22 + m.m32);
		m.m23 = 0.5f * (m.m23 + m.m33);
		return m;
	}

	/// <summary>
	/// 设置级联数据
	/// </summary>
	/// <param name="index">灯光索引</param>
	/// <param name="cullingSphere">剔除球</param>
	/// <param name="tileSize">阴影贴图在图集中的大小</param>
	void SetCascadeData(int index, Vector4 cullingSphere, float tileSize)
	{
		float texelSize = 2f * cullingSphere.w / tileSize; //世界剔除球直径 / 阴影贴图瓦片边长 = 一个纹素对应的世界空间大小
		float filterSize = texelSize * ((float)settings.directional.filter + 1f); //若滤波核增长，法线偏置需要一并增大，才能修复阴影痤疮
		cullingSphere.w -= filterSize; //增大采样区域，可能会采样到剔除球外的像素，所以提前缩小剔除球，以免错误采样
		cullingSphere.w *= cullingSphere.w; //记录半径的平方：用于计算点是否在球内：(x-cx)^2 + (y-cy)^2 + (z-cz)^2 ≤ r^2
		cascadeCullingSpheres[index] = cullingSphere;
		cascadeData[index] = new Vector4(
			1f / cullingSphere.w,   //传入半径的倒数
			filterSize * 1.4142136f	//单个纹素对应的世界空间大小 * 根号2：法线偏置考虑一个纹素对角线的长度，避免偏移长度不够
		);
	}

	/// <summary>
	/// 渲染单个灯光的阴影
	/// </summary>
	/// <param name="index">灯光索引</param>
	/// <param name="split">分割数</param>
	/// <param name="tileSize">阴影贴图在图集中的大小</param>
	void RenderDirectionalShadows(int index, int split, int tileSize)
	{
		ShadowedDirectionalLight light = ShadowedDirectionalLights[index]; //获取当前需要投射阴影的灯光信息
		var shadowSettings = new ShadowDrawingSettings(cullingResults, light.visibleLightIndex); //创建一个ShadowDrawingSettings结构体实例，包含了绘制阴影所需的一些设置和数据
		int cascadeCount = settings.directional.cascadeCount;
		int tileOffset = index * cascadeCount; //计算瓦片偏移
		Vector3 ratios = settings.directional.CascadeRatios; //获取级联比例
		
		//筛选因子：当阴影级联混合的比例越大，那么需要注意不要让前一个剔除球范围缩小太多导致的阴影错误；即：混合比例越大，缩小比例越小
		float cullingFactor = Mathf.Max(0f, 0.8f - settings.directional.cascadeFade);
		
		//渲染每个灯光的所有级联阴影贴图
		for(int i = 0; i < cascadeCount; i++)
		{
			cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives //计算阴影矩阵和剔除基本体
			(
			light.visibleLightIndex, //索引
			i, //瓦片索引
			cascadeCount, ratios, //阴影级联
			tileSize, //贴图大小
			light.nearPlaneOffset, //光源近平面偏移：让其范围内的物体不被阴影贴图渲染阴影
			out Matrix4x4 viewMatrix,  //视图矩阵：用于将场景中的物体从世界空间转换到光源空间，以便计算阴影
			out Matrix4x4 projectionMatrix, //投影矩阵：用于将光源空间中的物体投影到阴影贴图上，内部使用正交投影矩阵（因为方向光无位置属性，无法使用透视投影）
			out ShadowSplitData splitData //阴影分割数据：包含了阴影级联的分割信息
			);
			
			//允许 Unity 利用 Cascade 的覆盖关系进行额外剔除：如果一个阴影投射体会被后一个级联所渲染，那么当前级联不再渲染它，即适当缩放剔除球
			splitData.shadowCascadeBlendCullingFactor = cullingFactor;
			
			//shadowSettings.splitData = splitData; 在Unity6已经被弃用，将在创建ShadowRendererList被内部赋予

			//传递方向光剔除球体的数据
			if (index == 0)
			{
				SetCascadeData(i, splitData.cullingSphere, tileSize);
			}

			var shadowRenderList = context.CreateShadowRendererList(ref shadowSettings); //创建一个ShadowRendererList对象
			int tileIndex = tileOffset + i; //阴影贴图的偏移
			dirShadowMatrices[tileIndex] = ConvertToAtlasMatrix( //将当前灯光的阴影矩阵转换到图集空间
				projectionMatrix * viewMatrix, //计算当前灯光的阴影矩阵，从世界空间转换到阴影贴图空间
				SetTileViewport(tileIndex, split, tileSize), split //设置瓦片视口
			); 
			buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix); //设置视图矩阵和投影矩阵
			//设置阴影偏置，解决阴影痤疮问题：为所有片元设定一个足够大的偏置、斜率偏置处理倾斜表面误差更大的情况；但需要不停的试错
			//我们使用法线偏置解决平面阴影痤疮，并利用灯光提供的斜率偏置解决倾斜表面阴影痤疮
			buffer.SetGlobalDepthBias(0f, light.slopeScaleBias); 
			buffer.DrawRendererList(shadowRenderList);
			ExecuteBuffer();
			buffer.SetGlobalDepthBias(0f, 0f); //因为偏置对GPU光栅化的影响是全局的，所以仅在渲染阴影贴图时使用
		}
	}

	/// <summary>
	/// 渲染每个灯光的阴影
	/// </summary>
	void RenderDirectionalShadows()
	{
		int atlasSize = (int)settings.directional.atlasSize; //获取阴影贴图的大小
		//创建一个临时的渲染纹理（属性名，宽度，高度，深度缓冲位数，纹理过滤模式，渲染纹理格式）
		buffer.GetTemporaryRT(dirShadowAtlasId, atlasSize, atlasSize, 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
		buffer.SetRenderTarget(dirShadowAtlasId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
		buffer.ClearRenderTarget(true, true, Color.clear); //清理渲染缓冲区内部数据
		buffer.BeginSample(bufferName);
		ExecuteBuffer(); //提交命令

		//根据当前需要投射阴影的灯光数量来计算图集的分割数：
		//瓦片数量 = 需要投射阴影数量 * 级联阴影数
		int tiles = ShadowedDirectionalLightCount * settings.directional.cascadeCount;
		//如果瓦片数量<=1，则分割数为1，否则若瓦片<=4，则分割为2，否则为4；分别对应1，1分为4，1分为16；尽量只考虑2的幂次数
		int split = tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4; 
		int tileSize = atlasSize / split; //正方形子图边长大小，根据图集大小和分割数计算每个子图的大小

		//设置单个灯光的阴影贴图在图集中的位置和大小，渲染每个需要投射阴影的灯光的阴影
		for(int i = 0; i < ShadowedDirectionalLightCount; i++)
		{
			RenderDirectionalShadows(i, split, tileSize);
		}

		//将数据传入GPU
		buffer.SetGlobalInt(cascadeCountId, settings.directional.cascadeCount); //设置级联阴影数
		buffer.SetGlobalVectorArray(cascadeCullingSpheresId, cascadeCullingSpheres); //设置级联剔除球体数组
		buffer.SetGlobalVectorArray(cascadeDataId, cascadeData); //设置级联数据
		buffer.SetGlobalMatrixArray(dirShadowMatricesId, dirShadowMatrices); //将每个需要投射阴影的灯光的阴影矩阵传入GPU
																			 //buffer.SetGlobalFloat(shadowDistanceId, settings.maxDistance); //传入最大阴影距离
		float f = 1f - settings.directional.cascadeFade; //修改f以将平方渐变改为线性渐变
		buffer.SetGlobalVector(shadowDistanceFadeId, new Vector4(
			1f / settings.maxDistance, 1f / settings.distanceFade, //全局阴影强度
			1f / (1f - f * f)
		));
		SetKeywords(directionalFilterKeywords, (int)settings.directional.filter - 1); //启用不同的PCF关键字
		SetKeywords(directionalFilterKeywords, (int)settings.directional.cascadeBlend - 1); //启用不同的级联混合模式关键字
		buffer.SetGlobalVector(shadowAtlasSizeId, new Vector4(atlasSize, 1f / atlasSize)); //传递图集大小以及纹素大小
		buffer.EndSample(bufferName);
		ExecuteBuffer(); //提交命令
	}

	/// <summary>
	/// 启用不同的PCF关键字
	/// </summary>
	void SetKeywords(string[] keywords, int enabledIndex)
	{
		//int enabledIndex = (int)settings.directional.filter - 1; //着色器变体数组只设置了三个，因为默认会使用最小的滤波核来执行PCF
		for(int i = 0; i < keywords.Length; i++)
		{
			if(i == enabledIndex)
			{
				buffer.EnableShaderKeyword(keywords[i]); //启用关键字
			}
			else
			{
				buffer.DisableShaderKeyword(keywords[i]); //关闭关键字
			}
		}
	}

	/// <summary>
	/// 提交命令
	/// </summary>
	void ExecuteBuffer()
	{
		context.ExecuteCommandBuffer(buffer); //执行CommandBuffer中的命令
		buffer.Clear(); //清空CommandBuffer，以便下一次使用
	}

	/// <summary>
	/// 开始渲染
	/// </summary>
	public void Render()
	{
		if(ShadowedDirectionalLightCount > 0) //如果需要渲染阴影的方向光 > 0
		{
			RenderDirectionalShadows();
		}
		else //如果不需要渲染阴影
		{
			//创建一个1*1的虚拟纹理，用以避免之后执行纹理清除时无纹理而报错；也可以使用引用shader关键字来避免生成着色器变体
			buffer.GetTemporaryRT(dirShadowAtlasId, 1, 1, 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
			//设置渲染标签（将纹理渲染至该属性，不要在乎缓冲区中的数据，存储绘制数据）
			buffer.SetRenderTarget(dirShadowAtlasId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
			buffer.ClearRenderTarget(true, true, Color.clear); //清理渲染缓冲区内部数据
			ExecuteBuffer(); //提交渲染
		}
	}

	/// <summary>
	/// 清除临时渲染纹理和缓冲区
	/// </summary>
	public void Cleanup()
	{
		buffer.ReleaseTemporaryRT(dirShadowAtlasId); //释放临时的渲染纹理
		ExecuteBuffer(); //提交CommandBuffer中的内容
	}

}