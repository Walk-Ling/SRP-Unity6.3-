using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;   //包含常用数据结构的命名空间，如：List<T>

//RenderPipeline：继承自RenderPipeline的类，负责实现具体的渲染流程，即自定义SRP的核心类
public class CustomRenderPipeline : RenderPipeline
{
    //将单个相机的渲染封装到外部类中，使得核心类更清晰
    CameraRenderer renderer = new CameraRenderer();

    bool useDynamicBatching, useGPUInstancing; //提供动态批处理、GPU实例化可选项

    ShadowSettings shadowSettings; //阴影设置

	public CustomRenderPipeline(bool useDynamicBatching, bool useGPUInstancing, bool useSRPBatcher, ShadowSettings shadowSettings)
	{
        this.shadowSettings = shadowSettings;
        this.useDynamicBatching = useDynamicBatching;
        this.useGPUInstancing = useGPUInstancing;
        
		GraphicsSettings.useScriptableRenderPipelineBatching = useSRPBatcher; //启用SRP的批处理：默认开启；优先级最高
        GraphicsSettings.lightsUseLinearIntensity = true;   //光照使用线性颜色空间
	}

	//该类中需要重写Render方法，定义具体的渲染流程，这个方法会在每一帧被调用，负责执行渲染命令和处理渲染逻辑
	//参数：一个ScriptableRenderContext和一个Camera数组。
	//RP会按照顺序对每个Camera进行渲染，ScriptableRenderContext提供了执行渲染命令的接口，每个相机的渲染是独立的
	protected override void Render(ScriptableRenderContext context, List<Camera> cameras)
    {
        foreach (Camera camera in cameras)  //遍历每个相机，执行渲染逻辑
        {
            //循环体：调用CameraRenderer.Render()，将ScriptableRenderContext和当前Camera作为参数传递，执行具体的渲染
            renderer.Render(context, camera, useDynamicBatching, useGPUInstancing, shadowSettings);   
        }
    }
}
