using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;

//RenderPipelineAsset：继承自RenderPipelineAsset的类，负责创建RenderPipeline实例，即自定义SRP的核心类
//继承泛型类：RenderPipelineAsset<T>，其中T是我们自定义的RenderPipeline类，新版泛型类解决了很多旧版中一些需要手动管理的类型转换问题
//添加CreateAssetMenu属性，使得我们可以在Unity编辑器中通过右键菜单创建该RenderPipelineAsset的实例
[CreateAssetMenu(menuName = "Rendering/Custom Render Pipeline")]
public class CustomRenderPipelineAsset : RenderPipelineAsset<CustomRenderPipeline>
{
    //实例化动态批处理、GPU实例化、SRP批处理可选项
    [SerializeField]
    bool useDynamicBatching = true, useGPUInstancing = true, useSRPBatching = true;

    [SerializeField]
    ShadowSettings shadows = default;

    //该类中需要重写CreatePipeline方法，返回一个RenderPipeline实例，这个实例就是我们的SRP的核心，负责执行渲染流程
    //protected：确保该方法只被渲染管线相关的类访问
    protected override RenderPipeline CreatePipeline()
    {
        //CreatePipeline中会返回一个__CustomRenderPipeline__ 新实例
        return new CustomRenderPipeline(useDynamicBatching, useGPUInstancing, useSRPBatching, shadows);
    }
}
