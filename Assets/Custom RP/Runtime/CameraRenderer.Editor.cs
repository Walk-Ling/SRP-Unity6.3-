using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor; //包含了Unity编辑器相关的类和方法，只有在编辑器模式下才会使用到，gizmos功能需要这个命名空间
using UnityEditor.Profiling;
using UnityEngine.Profiling; //包含了Unity编辑器中与性能分析相关的类和方法，Profiler相关功能需要这个命名空间

//CameraRenderer：负责处理单个Camera的渲染逻辑
partial class CameraRenderer
{
    partial void DrawGizmos();

    partial void DrawUnsupportedShader(); //声明函数：当文件不处于编辑器模式下时，下面的函数体会被忽略，而另一个文件中仍然有调用，所以需要一个空声明

    partial void PrepareForSceneWindow();

    partial void PrepareBuffer();

#if UNITY_EDITOR    //在编辑器模式下，使用UnityEditor命名空间中的类和方法
    //旧着色器ID：当使用旧的着色器标签ID时，会进行错误的渲染，用以提醒开发者
    static ShaderTagId[] legacyShaderTagId =
    {
        new ShaderTagId("Always"),
        new ShaderTagId("ForwardBase"),
        new ShaderTagId("PrepassBase"),
        new ShaderTagId("Vertex"),
        new ShaderTagId("VertexLMRGBM"),
        new ShaderTagId("VertexLM")
    };

    static Material errorMaterial; //错误材质：用于给非支持的shader渲染一个明显的错误材质

    //绘制Gizmos：在编辑器中绘制Gizmos，方便开发
    partial void DrawGizmos()
    {
        //利用UnityEditor.Handles.ShouldRenderGizmos()来判断是否需要绘制，只有当前处于Unity编辑器的Scene视图，并且Gizmos按钮被激活时才返回true
        if(Handles.ShouldRenderGizmos())
        {
            RendererList gizmosRendererListPre = context.CreateGizmoRendererList(camera, GizmoSubset.PreImageEffects);
            RendererList gizmosRendererListPost = context.CreateGizmoRendererList(camera, GizmoSubset.PostImageEffects);
            buffer.DrawRendererList(gizmosRendererListPre);
            buffer.DrawRendererList(gizmosRendererListPost);
        }
    }

    //绘制不支持的shader：当使用旧的着色器标签ID时，会进行错误的渲染，用以提醒开发者
    partial void DrawUnsupportedShader()
    {
        if (errorMaterial == null)
        {
            errorMaterial = new Material(Shader.Find("Hidden/InternalErrorShader")); //使用Unity内置的错误着色器创建一个错误材质
        }

        var drawingSettings = new DrawingSettings(legacyShaderTagId[0], new SortingSettings(camera)) { overrideMaterial = errorMaterial }; //指定此次渲染使用的标签，并使用内置覆盖材质属性（overrideMaterial），来赋予错误材质
        //利用SetShaderPassName循环添加此次绘制可以接受的标签，确保一次渲染就能覆盖所有标签
        //将数组中的每个ID都设置为绘制设置的一个着色器通道，确保所有旧的着色器标签ID都会被错误地渲染
        for (int i = 1; i < legacyShaderTagId.Length; i++)
        {
            drawingSettings.SetShaderPassName(i, legacyShaderTagId[i]);
        }

        //由于这些是无效通道，结果无论如何都是错误的，所以我们不用关心其他设置。可以通过FilteringSettings.defaultValue属性获得默认筛选设置。
        var filteringSettings = FilteringSettings.defaultValue;
        Params.drawSettings = drawingSettings;
        Params.filteringSettings = filteringSettings;
        RendererList unsupportedRendererList = context.CreateRendererList(ref Params);
        buffer.DrawRendererList(unsupportedRendererList);
    }

    //将UI渲染到Scene视图中
    partial void PrepareForSceneWindow()
    {
        if(camera.cameraType == CameraType.SceneView)   //如果当前渲染的相机是Scene视图的相机，那么就渲染UI在Scene视图中
        {
            //EmitWorldGeometryForSceneView方法会将场景中的UI元素（如Gizmos、Handles等）渲染到Scene视图中，使得开发者在编辑器中能够看到这些元素
            ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
        }
    }

    //采样名称：用于在帧调试器中显示当前渲染阶段的名称，方便调试和分析性能
    string SampleName { get; set; } //自动属性：编译器会自动生成一个私有的字段来存储属性值，并提供默认的get和set方法，简化代码编写

    //准备编辑模式专用的渲染缓冲区：处理编辑器模式下的特殊渲染需求
    partial void PrepareBuffer()
    {
        Profiler.BeginSample("Editor Only"); //在Profiler中开始一个新的采样，给与标记"Editor Only"，方便在性能分析中区分编辑器模式下的渲染阶段
        buffer.name = SampleName = camera.name; //将CommandBuffer的名字设置为当前渲染的相机的名字，方便在Profiler中调试
        Profiler.EndSample(); //结束当前采样
    }
#else
    //camera.name：在访问时，会在堆上分配新的字符串内存。在游戏运行的每一帧都这样做，会给垃圾回收器带来压力，造成性能问题。
    //在非编辑器模式下，采样名称直接使用命令缓冲区的名称
    string SampleName => bufferName; //=>：表达式主体成员：简化属性或方法的定义，使代码更简洁，等价于为属性或方法编写一个完整的get方法
#endif
}

