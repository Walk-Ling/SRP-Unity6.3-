using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/*
	用于自定义Shader的Inspector界面，可以在这里添加一些自定义的属性和功能；
	必须继承自ShaderGUI类，并重写OnGUI方法来实现自定义的界面逻辑；
	在OnGUI方法中，可以使用EditorGUILayout等方法来绘制界面元素，并通过MaterialEditor来修改材质属性；
	可以根据需要添加一些自定义的功能，比如预览窗口、材质属性分组等；
	最后，在Shader文件中使用CustomEditor属性来指定使用这个自定义的ShaderGUI类。
*/
public class CustomShaderGUI : ShaderGUI
{
	bool showPresets; //用于控制预设选项的显示与隐藏

	MaterialEditor editor;  //负责绘制和管理当前 Material Inspector 的编辑器对象
	Object[] materials;     //使用同一着色器的多个材质可以同时编辑，当前 Inspector 正在编辑的所有材质实例
	MaterialProperty[] properties;	//当前材质的所有属性列表

	/// <summary>
	/// 阴影模式
	/// </summary>
	enum ShadowMode
	{
		On, Clip, Dither, Off
	}

	ShadowMode Shadows
	{
		set
		{
			if (SetProperty("_Shadows", (float)value)) //返回 true = 属性存在 + 值发生了变化并成功写入
			{
				//当值与相对的关键字对应，则设置关键字
				SetKeyword("_SHADOWS_CLIP", value == ShadowMode.Clip); 
				SetKeyword("_SHADOWS_DITHER", value == ShadowMode.Dither);
			}
		}
	}
 
	/// <summary>
	/// 重写OnGUI来实现自定义的界面
	/// </summary>
	public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
	{
		EditorGUI.BeginChangeCheck(); //记录UI控件的状态
		base.OnGUI(materialEditor, properties);
		editor = materialEditor;
		materials = materialEditor.targets;
		this.properties = properties;
		
		EditorGUILayout.Space(); //在Inspector界面中添加一个空白区域，以增加界面元素之间的间距
		//创建一个可折叠的标题为"Presets"的折叠区域，并将其展开状态绑定到showPresets变量上，默认为false(折叠)
		//Foldout(折叠状态，标签名，是否将标签名也纳入点击区域)
		showPresets = EditorGUILayout.Foldout(showPresets, "Presets", true);
		if(showPresets)
		{
			OpaquaPreset();
			ClipPreset();
			FadePreset();
			TransparentPreset();
		}
		
		if (EditorGUI.EndChangeCheck()) //若发生变更则重新执行判断是否投射阴影
		{
			SetShadowCasterPass();	
		}
	}

	//根据属性名称获取/修改属性值
	bool SetProperty(string name, float value)
	{
		//在给定的属性列表中查找指定名称的属性，最后一个参数表示该属性是否必须存在，如果为true且找不到该属性，则会抛出异常，若不填则默认为true
		MaterialProperty property = FindProperty(name, properties, false);
		if(property != null)
		{
			property.floatValue = value; //将其值设置为给定的浮点数
			return true;
		}

		return false; //如果找不到该属性，则返回false
	}

	//设置关键字
	void SetKeyword(string keyword, bool enabled)
	{
		if(enabled)
		{
			foreach(Material m in materials) //遍历当前正在编辑的所有材质实例，即使用同一着色器的多个材质
			{
				m.EnableKeyword(keyword);	//启用指定的关键字
			}
		}
		else
		{
			foreach (Material m in materials)
			{
				m.DisableKeyword(keyword);	//禁用指定的关键字
			}
		}
	}

	//切换属性-关键字组合的 SetProperty 变体
	void SetProperty(string name, string keyword, bool value)
	{
		if (SetProperty(name, value ? 1f : 0f)) //将bool转换为float值，设置初始化属性值
		{
			SetKeyword(keyword, value); //根据bool值启用或禁用关键字
		}
	}

	//属性设置器
	bool Clipping { set => SetProperty("_Clipping", "_CLIPPING", value); }	//透明度裁剪设置

	bool PremultiplyAlpha { set => SetProperty("_PremulAlpha", "_PREMULTIPLY_ALPHA", value); } //预乘Alpha设置

	//BlendMode枚举类型定义了不同的混合模式选项，可以用于设置源混合模式和目标混合模式
	BlendMode SrcBlend { set => SetProperty("_SrcBlend", (float)value); } //源混合模式设置

	BlendMode DstBlend { set => SetProperty("_DstBlend", (float)value); } //目标混合模式设置

	bool ZWrite { set => SetProperty("_ZWrite", value ? 1f : 0f); } //深度写入设置

	//RenderQueue枚举类型定义了不同的渲染队列选项，相当于用float来设置材质的渲染队列，枚举值会被转换为整数并设置到材质中
	RenderQueue RenderQueue 
	{
		set 
		{
			foreach (Material m in materials)
			{
				m.renderQueue = (int)value; //将枚举值转换为整数，并设置材质的渲染队列
			}
		} 
	}

	//预设按钮
	bool PresetButton(string name)
	{
		if(GUILayout.Button(name)) //根据给定的名称创建一个按钮，并检查用户是否点击了该按钮
		{
			editor.RegisterPropertyChangeUndo(name); //注册属性更改的撤销操作，允许用户撤销对材质属性所做的更改
			return true;
		}
		return false;
	}

	//不透明物体预设
	void OpaquaPreset()
	{
		if(PresetButton("Opaque"))
		{
			Clipping = false;
			PremultiplyAlpha = false;
			SrcBlend = BlendMode.One;
			DstBlend = BlendMode.Zero;
			ZWrite = true;
			RenderQueue = RenderQueue.Geometry;
			Shadows = ShadowMode.On;
		}
	}

	//透明度裁剪预设
	void ClipPreset()
	{
		if(PresetButton("Clip"))
		{
			Clipping = true;
			PremultiplyAlpha = false;
			SrcBlend = BlendMode.One;
			DstBlend = BlendMode.Zero;
			ZWrite = true;
			RenderQueue = RenderQueue.AlphaTest;
			Shadows = ShadowMode.Clip;
		}
	}

	//透明度混合预设
	void FadePreset()
	{
		if (PresetButton("Fade (透明度混合)"))
		{
			Clipping = false;
			PremultiplyAlpha = false;
			SrcBlend = BlendMode.SrcAlpha;
			DstBlend = BlendMode.OneMinusSrcAlpha;
			ZWrite = false;
			RenderQueue = RenderQueue.Transparent;
			Shadows = ShadowMode.Dither;
		}
	}

	//用以兼容UnlitShader的函数
	//检查属性是否存在于当前材质
	bool HasProperty(string name) => FindProperty(name, properties, false) != null;

	bool HasPremultiplyAlpha => HasProperty("_PremulAlpha"); //检查是否存在预乘Alpha属性

	//预乘透明度混合预设
	void TransparentPreset()
	{
		if (HasPremultiplyAlpha && PresetButton("Transparent (预乘透明度)"))
		{
			Clipping = false;
			PremultiplyAlpha = true;
			SrcBlend = BlendMode.One;   //预乘Alpha在计算表面反射颜色时已经将源颜色*源Alpha了，所以混合时使用One混合
			DstBlend = BlendMode.OneMinusSrcAlpha;
			ZWrite = false;
			RenderQueue = RenderQueue.Transparent;
			Shadows = ShadowMode.Dither;
		}
	}

	/// <summary>
	/// 根据材质的 ShadowMode，决定这个材质“要不要参与阴影投射
	/// </summary>
	void SetShadowCasterPass()
	{
		MaterialProperty shadows = FindProperty("_Shadows", properties, false);
		if (shadows == null || shadows.hasMixedValue) //若未找到材质属性 || 该属性有多种值
		{
			return;
		}
		bool enabled = shadows.floatValue < (float)ShadowMode.Off; //是否启用阴影
		foreach (Material m in materials) //遍历所有选中的材质
		{
			m.SetShaderPassEnabled("ShadowCaster", enabled);
		}
	}
}