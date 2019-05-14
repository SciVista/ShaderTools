using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
using UnityEditor;
using System.Linq;
using Sirenix.OdinInspector.Editor;
using System.IO;
using System;


public class ShaderControlCreator : OdinEditorWindow
{

    [MenuItem("SciVista Tools/Shader Controler Tool")]
    private static void OpenWindow()
    {
        GetWindow<ShaderControlCreator>().Show();
    }

    [SerializeField]
    private bool MakeRPCS = false;
    [SerializeField]
    private bool OnValueChangedAttribute = true;


    [SerializeField]
    private Shader _target;

    public Shader Target { get => _target; set => _target = value; }

    public string TargetName;

    [SerializeField, ReadOnly]
    private List<ShaderProperty> TargetProperties;

    [SerializeField, FilePath]
    private string templatePath;

    [SerializeField, ListDrawerSettings(ShowItemCount = true, ShowIndexLabels = true)]
    private List<string> FileTemplate;

    [Serializable]
    public struct ShaderProperty
    {
        [SerializeField]
        private string _propertyName;
        [SerializeField]
        private ShaderUtil.ShaderPropertyType _propertyType;
        [SerializeField]
        private string _propertyDescription;
        [SerializeField]
        private int _iD;

        public string PropertyName { get => _propertyName; set => _propertyName = value; }
        public ShaderUtil.ShaderPropertyType PropertyType { get => _propertyType; set => _propertyType = value; }
        public string PropertyDescription { get => _propertyDescription; set => _propertyDescription = value; }
        public int ID { get => _iD; set => _iD = value; }

    }

    public void GetShaderInfo()
    {
        int count = ShaderUtil.GetPropertyCount(Target);
        TargetProperties = new List<ShaderProperty>(count);
        for (int i = 0; i < count; i++)
        {
            TargetProperties.Add(new ShaderProperty() {
            PropertyName = ShaderUtil.GetPropertyName(Target, i),
            PropertyType = ShaderUtil.GetPropertyType(Target, i),
            PropertyDescription = ShaderUtil.GetPropertyDescription(Target, i),
            ID = i
            });
        }
        Debug.LogFormat("Shader: {0}\nValid: {1}\nProperties: {2}\n", Target.name, Target.isSupported, count);
    }

    public void ReadTemplate()
    {
        string[] templateLines = File.ReadAllLines(templatePath);
        FileTemplate = new List<string>(templateLines.Length - 1 + 4*TargetProperties.Count);
        foreach(string line in templateLines)
        {
            FileTemplate.Add(line);
            Debug.Log(line);
        }
        FileTemplate[templateLines.Length - 1] = "";
        //Add MeshRenderer and Filter property
        FileTemplate.Add(SERIALZE);
        FileTemplate.Add(string.Format("{0}private MeshRenderer meshRenderer;", Space4));
        FileTemplate.Add(SERIALZE);
        FileTemplate.Add(string.Format("{0}private MeshFilter meshFilter;", Space4));
    }

    public void AddMethodTemplates()
    {
        foreach(ShaderProperty property in TargetProperties)
        {
            FileTemplate.AddRange(ShaderPropertyToMethod(property, MakeRPCS, OnValueChangedAttribute));
        }
        TargetName = Target.name.Split('/').Last();
        string className = string.Format("{0}_Controller", TargetName);
        FileTemplate[5] = FileTemplate[5].Replace(DefaultScriptName, className);
        FileTemplate[10] = FileTemplate[10].Replace(NOTRIM, "");
        FileTemplate[16] = FileTemplate[16].Replace(NOTRIM, "");
        FileTemplate.Add("}");  
    }

    const string DefaultScriptName = "#SCRIPTNAME#";
    const string NOTRIM = "#NOTRIM#";
    const string OpenBracket  = "    {";
    const string CloseBracket = "    }";
    const string RPC = "    [PunRPC]";
    const string SERIALZE = "    [SerializeField]";
    const string Space4 = "    ";
    const string Space8 = "        ";
    const string FloatArg  = "float value";
    const string VectorArg = "Vector4 value";
    const string ColorArg  = "Color value";
    const string TexArg    = "Texture2D value";
    const string FloatSet  = "SetFloat";
    const string VectorSet = "SetVector";
    const string ColorSet  = "SetColor";
    const string TexSet    = "SetTexture";
    const string FLOAT = "float";
    const string VECTOR4 = "Vector4";
    const string COLOR = "Color";
    const string TEX = "Texture";
    private static string[] ShaderPropertyToMethod(ShaderProperty property, bool makeRPC, bool OnValueChanged)
    {
        string argType = "";
        string setType = "";
        string varType = "";
        switch (property.PropertyType)
        {
            case ShaderUtil.ShaderPropertyType.Float:
            case ShaderUtil.ShaderPropertyType.Range:
                argType = FloatArg;
                setType = FloatSet;
                varType = FLOAT;
                break;
            case ShaderUtil.ShaderPropertyType.Vector:
                argType = VectorArg;
                setType = VectorSet;
                varType = VECTOR4;
                break;
            case ShaderUtil.ShaderPropertyType.Color:
                argType = ColorArg;
                setType = ColorSet;
                varType = COLOR;
                break;
            case ShaderUtil.ShaderPropertyType.TexEnv:
                argType = TexArg;
                setType = TexSet;
                varType = TEX;
                break;
        }
        List<string> method = new List<string>(makeRPC ? 8 : 7);
        string nicePropName = property.PropertyDescription.Replace("(", "").Replace(")", "").Replace(",", "").Replace(" ", "");
        string methodName = string.Format("Set_{0}({1})", nicePropName, argType);



        //Add Variable
        method.Add(SERIALZE);
        if (OnValueChanged) method.Add(string.Format("{0}[OnValueChanged(\"{1}\")]", Space4, methodName.Split('(')[0] + "_Shader"));
        method.Add(string.Format("{0}public {1} {2};", Space4, varType, nicePropName));

        //Add Variable Control Method
        if (makeRPC) method.Add(RPC);
        method.Add(string.Format("{0}public void {1}", Space4, methodName));
        method.Add(OpenBracket);
        method.Add(string.Format("{0}{1} = value;", Space8, nicePropName));
        method.Add(CloseBracket);
        method.Add("");

        //Add Shader Property Control Method
        if (makeRPC) method.Add(RPC);
        methodName = string.Format("Set_{0}_Shader()", nicePropName);
        method.Add(string.Format("{0}public void {1}", Space4, methodName));
        method.Add(OpenBracket);
        method.Add(string.Format("{0}meshRenderer.sharedMaterial.{1}(\"{2}\", {3});", Space8, setType, property.PropertyName, nicePropName));
        method.Add(CloseBracket);
        method.Add("");

        return method.ToArray();
    }

    [Button(ButtonSizes.Large)]
    public void CreateShaderControl()
    {
        GetShaderInfo();
        ReadTemplate();
        AddMethodTemplates();
        string outFileName = string.Format("{0}_Controller.cs", TargetName);
        string outPath = Path.Combine(Application.dataPath, "AutoGeneratedScripts", outFileName);
        File.WriteAllLines(outPath, FileTemplate);
        Debug.LogFormat("Wrote {0} to {1}", outFileName, outPath);

        if (TargetProperties == null || TargetProperties.Count == 0) throw new NullReferenceException();

        foreach(var property in TargetProperties)
        {

        }

    }


}
