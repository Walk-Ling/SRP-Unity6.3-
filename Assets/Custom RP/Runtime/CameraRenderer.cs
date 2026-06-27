using System;
using UnityEngine;
using UnityEngine.Rendering;    //SRP的核心命名空间，包含了所有与渲染相关的类和结构体

//CameraRenderer局部类：将CameraRenderer类分成多个文件，方便管理和维护
/*
 * partial
 * 什么是局部类？
 * 这是一种将类或结构定义拆分为多个部分的方法，分别存储在不同的文件中，它唯一的目的就是组织代码。
 * 典型的用例是将自动生成的代码与手工编写的代码分开。就编译器而言，它都是同一个类定义的一部分。
*/
public partial class CameraRenderer
{
    //1、成员变量：ScriptableRenderContext和Camera，分别用于管理渲染流程和存储当前渲染的相机信息
    ScriptableRenderContext context;
    Camera camera;

    //2、创建CommandBuffer：用于存储渲染命令的对象，可以在不同阶段执行这些命令，现代Unity使用CommandBuffer来管理渲染命令
    const string bufferName = "Render Camera";
    CommandBuffer buffer = new CommandBuffer { name = bufferName };

    CullingResults cullingResults; //剔除结果：包含了相机视野内的所有可见对象的信息

    //着色器标签ID：用于标识特定类型的着色器，方便在渲染过程中选择和管理不同的着色器
    static ShaderTagId
        unlitShaderTagId = new ShaderTagId("SRPDefaultUnlit"),   //将SRPDefaultUnlit作为ID
        litShaderTagId = new ShaderTagId("CustomLit");  //自定义光照标签

    //使用一个类似列表的结构体来管理渲染对象，包含剔除结果、绘制设置和过滤设置等信息
    RendererListParams Params = new RendererListParams();

    Lighting lighting = new Lighting();  //创建一个Lighting实例

    public void Render(ScriptableRenderContext context, Camera camera, bool useDynamicBatching, bool useGPUInstancing, ShadowSettings shadowSettings) //提供动态批处理、GPU实例化可选项
    {
        this.context = context;
        this.camera = camera;

        PrepareBuffer(); //准备渲染命令缓冲区，设置命令缓冲区的名称，方便调试不同的摄像机渲染阶段
        PrepareForSceneWindow(); //将UI绘制到Scene视图中，因为可能有新的物体，所以需要在剔除之前调用

        //执行剔除操作
        if (!Cull(shadowSettings.maxDistance))
        {
            return; //剔除失败不进行渲染，直接返回
        }

        buffer.BeginSample(SampleName); //将阴影包含到Camera的FrameDebbug标签中
        ExecuteBuffer();
        lighting.Setup(context, cullingResults, shadowSettings);    //设置光照属性
        buffer.EndSample(SampleName);
		Setup(); //设置相机属性，准备渲染，阴影渲染和相机渲染都拥有独立的渲染标签，所以渲染完阴影再转到相机渲染以保证渲染标签正确，否则后续的物体渲染会渲染到阴影贴图中
		DrawVisibleGeometry(useDynamicBatching, useGPUInstancing);  //绘制相机可见的几何体
        DrawUnsupportedShader(); //绘制不支持的shader
        DrawGizmos(); //绘制Gizmos
        lighting.Cleanup(); //清除临时渲染纹理和CommandBuffer缓冲区
        Submit();   //提交渲染命令，执行渲染
    }

    //剔除：剔除不在相机视野内的对象
    bool Cull(float maxShadowDistance)
    {
        //它是一个包含着色器级细节参数的结构体，由摄像机生成。里面包括了执行剔除时需要的所有数学和场景信息
        //ScriptableCullingParameters p;
        //手动填充这个结构体会非常繁琐且容易出错。
        //Unity 提供了 TryGetCullingParameters 方法，让你可以直接从摄像机那里“拷贝”一份正确配置好的参数。
        //Try-get模式：进行一次尝试获取，可能成功或失败，返回bool值，并通过out传参获取结果
        //out参数：被传入该变量的函数必须重新赋值它，并在函数结束时返回该参数，即相当于一个强制重新赋值的引用传递
        if (camera.TryGetCullingParameters(out ScriptableCullingParameters p))
        {
            p.shadowDistance = Mathf.Min(maxShadowDistance, camera.farClipPlane); //比较阴影距离和相机的远裁剪面，取较小值，确保阴影不会超过相机的可见范围
            //经过TryGetCullingParameters(out p)的调用，p相当于被正确初始化剔除相关的参数，可以用来执行剔除操作了
            //执行真正的剔除操作，用ref引用传递参数p避免内存浪费，剔除结果存储在CullingResults结构体中，包含所有可见的对象信息
            cullingResults = context.Cull(ref p);
            return true;
        }
        //false：如果相机无法提供有效的剔除参数（例如相机被禁用/没有正确设置），则剔除失败
        return false;
    }

    //设置摄像机的属性
    void Setup()
    {
        //设置摄像机属性：视图矩阵和投影矩阵，确保正确渲染，相当于unity_MatrixVP
        context.SetupCameraProperties(camera);

        //CameraClearFlags枚举：定义了相机在渲染前如何清理屏幕的选项，决定了渲染前如何清理屏幕，1~4消除范围递减
        //1、Skybox：清除颜色缓冲区和深度缓冲区，并使用相机的天空盒来渲染空白部分
        //2、SolidColor/Color：清除颜色缓冲区和深度缓冲区，使用相机的背景颜色填充屏幕
        //3、Depth：清除深度缓冲区，但保留颜色缓冲区的内容，适用于需要在多个相机之间叠加渲染的情况
        //4、Nothing：不清除任何缓冲区，可能会导致残留的图像或深度信息
        CameraClearFlags flags = camera.clearFlags; //获取相机的清除标志，决定了渲染前如何清理屏幕
        //清除渲染目标，准备新的渲染帧，参数：是否清理深度缓冲区、是否清理颜色缓冲区、清理颜色的值
        buffer.ClearRenderTarget(
            flags <= CameraClearFlags.Depth,    //如果清除标志<=Depth，说明需要清除深度缓冲区
            flags <= CameraClearFlags.Color,    //如果清除标志<=SolidCOlor/Clear，说明需要清除颜色缓冲区
            flags == CameraClearFlags.Color ? camera.backgroundColor.linear : Color.clear   //如果清除标志是SolidColor，使用相机的背景颜色，否则使用透明色清理颜色缓冲区
            );
        buffer.BeginSample(SampleName); //开始一个新的采样，标记渲染命令的开始，方便调试和性能分析，在Profiler中查看
        ExecuteBuffer();
    }

    //绘制该相机可见的所有几何体
    void DrawVisibleGeometry(bool useDynamicBatching, bool useGPUInstancing) //添加动态批处理、GPU实例化可选项
    {
        //绘制不透明几何体
        var sortingSettings = new SortingSettings(camera) { criteria = SortingCriteria.CommonOpaque }; //排序设置结构体：包含了渲染对象的排序方式、相机等信息，决定了渲染对象的绘制顺序
        var drawingSettings = new DrawingSettings(unlitShaderTagId, sortingSettings) {
            enableDynamicBatching = useDynamicBatching, //动态批处理：启用
            enableInstancing = useGPUInstancing //GPU实例化：关闭；GPU实例化的优先级要高于动态批处理，SRP批处理优先级最高
        };    //绘制设置结构体：包含了渲染对象的排序、着色器通道等信息
        drawingSettings.SetShaderPassName(1, litShaderTagId); //在可渲染标签ID数组中添加新的标签
        var filteringSettings = new FilteringSettings(RenderQueueRange.opaque);    //过滤设置结构体：包含了渲染对象的层级、渲染队列等信息
        
        //1、RendererListParams：使用一个类似列表的结构体来管理渲染对象，包含剔除结果、绘制设置和过滤设置等信息
        //context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
        Params = new RendererListParams(cullingResults, drawingSettings, filteringSettings);
        //2、创建RendererList
        RendererList opaquaRendererList = context.CreateRendererList(ref Params);
        //3、将RendererList添加到CommandBuffer中，准备执行渲染命令
        buffer.DrawRendererList(opaquaRendererList);


        //绘制Skybox
        //1、创建RenderList：现代Unity中使用RenderList来管理渲染对象
        RendererList SkyboxRenderList = context.CreateSkyboxRendererList(camera);
        //2、创建CommandBuffer：用于存储渲染命令的对象，可以在不同阶段执行这些命令，现代Unity使用CommandBuffer来管理渲染命令
        //CommandBuffer buffer = new CommandBuffer { name = "Render Skybox" };
        buffer.DrawRendererList(SkyboxRenderList); //将RenderList添加到CommandBuffer中，准备执行渲染命令
        //3、执行CommandBuffer：将CommandBuffer中的命令提交给GPU执行
        //context.ExecuteCommandBuffer(buffer);
        //4、清理/释放CommandBuffer：避免内存泄漏
        //buffer.Release();


        //绘制透明物体
        //1、修改排序设置：将排序方式该为透明物体的排序方式，确保正确渲染
        sortingSettings.criteria = SortingCriteria.CommonTransparent;
        //2、修改绘制设置：使用相同的着色器标签ID，但更新排序设置，确保正确渲染
        drawingSettings.sortingSettings = sortingSettings;
        //3、修改过滤设置：仅容许渲染透明物体
        filteringSettings.renderQueueRange = RenderQueueRange.transparent;
        //4、修改RendererListParams：更新上述设置
        Params.drawSettings = drawingSettings;
        Params.filteringSettings = filteringSettings;
        //5、创建RendererList：使用更新后的设置创建一个新的RendererList，包含所有可见的透明对象
        RendererList transparentRendererList = context.CreateRendererList(ref Params);
        //6、将RendererList添加到CommandBuffer中
        buffer.DrawRendererList(transparentRendererList);
    }

    //提交渲染命令：将所有准备好的渲染命令提交给GPU执行，确保渲染结果正确显示在屏幕上
    void Submit()
    {
        buffer.EndSample(SampleName); //结束采样，标记渲染命令的结束
        ExecuteBuffer();
        context.Submit();
    }

    //3、执行CommandBuffer：将CommandBuffer中的命令提交给GPU执行
    void ExecuteBuffer()
    {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear(); //清理buffer，准备下一帧的渲染命令
    }
}
