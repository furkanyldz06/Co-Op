using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ShadowMeter
{
    [CustomEditor(typeof(ShadowMeterScript))]
    public class EditorLightDetectorScript : Editor
    {
        public VisualTreeAsset visualTree;
        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = new VisualElement();
            visualTree.CloneTree(root);
            return root;
        }
    }
}
