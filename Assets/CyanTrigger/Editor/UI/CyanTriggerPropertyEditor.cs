using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;
using Object = UnityEngine.Object;

namespace Cyan.CT.Editor
{
    public static class CyanTriggerPropertyEditor
    {
        private const float FoldoutListHeaderHeight = 16;
        private const float FoldoutListHeaderAreaHeight = 19;
        
        private static GUIStyle _footerButtonStyle;
        private static GUIStyle _footerBackgroundStyle;
        private static GUIStyle _headerBackgroundStyle;

        private static readonly MethodInfo SetBoldDefaultFont = 
            typeof(EditorGUIUtility).GetMethod("SetBoldDefaultFont", BindingFlags.Static | BindingFlags.NonPublic);

        private static void SetBoldFont(bool bold)
        {
            SetBoldDefaultFont.Invoke(null, new[] { bold as object });
        }
        
        public static bool DrawEditor(
            SerializedProperty dataProperty,
            Rect rect,
            GUIContent variableName,
            Type type,
            bool layout = false,
            Func<List<(GUIContent, object)>> getConstInputOptionsFunc = null)
        {
            bool multi = EditorGUI.showMixedValue;
            bool shouldShowMixed = dataProperty.hasMultipleDifferentValues;
            
            if (!layout)
            {
                EditorGUI.BeginProperty(rect, GUIContent.none, dataProperty);
            }
            else if (dataProperty.isInstantiatedPrefab)
            {
                EditorGUI.showMixedValue = shouldShowMixed;
                SetBoldFont(dataProperty.prefabOverride);
            }

            // Prevent getting the data on layout events and only use default value for type.
            bool isLayoutEvent = Event.current?.type == EventType.Layout;
            object obj = isLayoutEvent || (shouldShowMixed && typeof(Object).IsAssignableFrom(type))
                ? GetDefaultForType(type)
                : CyanTriggerSerializableObject.ObjectFromSerializedProperty(dataProperty);
            
            bool dirty = false;
            obj = DisplayPropertyEditor(rect, variableName, type, obj, ref dirty, layout, getConstInputOptionsFunc);

            if (!isLayoutEvent && dirty)
            {
                CyanTriggerSerializableObject.UpdateSerializedProperty(dataProperty, obj);
            }
            
            if (!layout)
            {
                EditorGUI.EndProperty();
            }
            else
            {
                SetBoldFont(false);
                EditorGUI.showMixedValue = multi;
            }

            return dirty;
        }

        public static bool DrawArrayEditor(
            SerializedProperty dataProperty, 
            GUIContent variableName, 
            Type type, 
            ref bool arrayExpand, 
            ref ReorderableList list, 
            bool layout = true, 
            Rect rect = default)
        {
            bool multi = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = dataProperty.hasMultipleDifferentValues;
            
            if (!layout)
            {
                EditorGUI.BeginProperty(rect, GUIContent.none, dataProperty);
            }
            else if (dataProperty.isInstantiatedPrefab)
            {
                SetBoldFont(dataProperty.prefabOverride);
            }
            
            object obj = CyanTriggerSerializableObject.ObjectFromSerializedProperty(dataProperty);
            bool dirty = false;
            obj = DisplayArrayPropertyEditor(variableName, type, obj, ref dirty, ref arrayExpand, ref list, layout, rect);

            if (dirty)
            {
                Type elementType = type.GetElementType();
                if(typeof(Object).IsAssignableFrom(elementType))
                {
                    var array = (Array) obj;
                    
                    // ReSharper disable once AssignNullToNotNullAttribute
                    Array destinationArray = Array.CreateInstance(elementType, array.Length);
                    Array.Copy(array, destinationArray, array.Length);
                
                    obj = destinationArray;
                }
                
                CyanTriggerSerializableObject.UpdateSerializedProperty(dataProperty, obj);
            }
            
            if (!layout)
            {
                EditorGUI.EndProperty();
            }
            else
            {
                SetBoldFont(false);
            }
            
            EditorGUI.showMixedValue = multi;
            
            return dirty;
        }

        /*
        public static bool DrawEditor(ref SerializableObject serializedObject, Rect rect, string variableName, Type type, ref bool arrayExpand, ref ReorderableList list)
        {
            bool dirty = false;
            object obj = DisplayPropertyEditor(rect, new GUIContent(variableName), type, serializedObject.obj, ref dirty, ref arrayExpand, ref list);

            if (dirty)
            {
                serializedObject.obj = obj;
            }

            return dirty;
        }
        */

        public static bool TypeHasSingleLineEditor(Type type)
        {
            return
                !type.IsArray
                && type != typeof(ParticleSystem.MinMaxCurve) 
                && type != typeof(Matrix4x4)
                && type != typeof(Bounds)
                && type != typeof(Rect)
                && type != typeof(Ray)
                && type != typeof(Plane);
        }
        
        public static bool TypeHasInLineEditor(Type type)
        {
            return !type.IsArray;
        }
        
        public static float HeightForInLineEditor(Type variableType)
        {
            if (TypeHasSingleLineEditor(variableType))
            {
                return EditorGUIUtility.singleLineHeight;
            }

            if (!variableType.IsArray)
            {
                int lines = -1;
                if (variableType == typeof(ParticleSystem.MinMaxCurve))
                {
                    lines = 3;
                }
                if (variableType == typeof(Matrix4x4))
                {
                    lines = 4;
                }
                if (variableType == typeof(Bounds)
                    || variableType == typeof(Rect)
                    || variableType == typeof(Ray)
                    || variableType == typeof(Plane))
                {
                    lines = 2;
                }

                if (lines == -1)
                {
                    throw new NotSupportedException($"Cannot calculate line height for type: {variableType}");
                }
                
                return EditorGUIUtility.singleLineHeight * lines + ((lines-1) * 2); 
            }

            throw new NotSupportedException($"Array types are not supported in line: {variableType}");
        }
        
        // TODO make a better api that doesn't take a list...
        public static float HeightForEditor(Type variableType, object variableValue, bool showList, ref ReorderableList list)
        {
            if (!variableType.IsArray)
            {
                return HeightForInLineEditor(variableType);
            }

            float height = FoldoutListHeaderAreaHeight;
            if (showList)
            {
                Type elementType = variableType.GetElementType();
                CreateReorderableListForVariable(elementType, variableValue as Array, ref list);
                height += list.GetHeight();
            }

            return height;
        }

        public static object CreateInitialValueForType(Type type, object variableValue, ref bool dirty)
        {
            // Check is required for the case when a destroyed object is still saved, but shouldn't be. 
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (typeof(Object).IsAssignableFrom(type) 
                && variableValue != null 
                && (!(variableValue is Object valueObj) || valueObj == null))
            {
                dirty = true;
                return null;
            }
            
            if(!type.IsInstanceOfType(variableValue))
            {
                object value = GetDefaultForType(type);
                if (value != variableValue)
                {
                    dirty = true;
                    variableValue = value;
                }
            }

            return variableValue;
        }
        
        public static object ResetToDefaultValue(Type type, object value, ref bool dirty)
        {
            value = CreateInitialValueForType(type, value, ref dirty);
            if (dirty)
            {
                return value;
            }

            if(type.IsValueType)
            {
                object defaultValue = GetDefaultForType(type);
                if (!defaultValue.Equals(value))
                {
                    dirty = true;
                }
                return defaultValue;
            }
            if (type.IsArray)
            {
                if (!(value is Array array) || array.Length != 0)
                {
                    value = GetDefaultForType(type);
                    dirty = true;
                }
                return value;
            }
            if (type == typeof(Gradient) && value == null)
            {
                dirty = true;
                return new Gradient();
            }

            if (value != null)
            {
                dirty = true;
            }
            
            return null;
        }

        public static object GetDefaultForType(Type type)
        {
            if(type.IsValueType)
            {
                return Activator.CreateInstance(type);
            }
            if (type.IsArray)
            {
                return Array.CreateInstance(type.GetElementType(), 0);
            }
            if (type == typeof(Gradient))
            {
                return new Gradient();
            }
            return null;
        }
        
        public static object DisplayPropertyEditor(
            Rect rect, 
            GUIContent content, 
            Type variableType, 
            object variableValue, 
            ref bool dirty, 
            bool layout = false,
            Func<List<(GUIContent, object)>> getConstInputOptions = null)
        {
            if (variableType.IsArray)
            {
                Debug.LogWarning("Trying to display an array type using the object method!");
                return variableValue;
            }

            variableValue = CreateInitialValueForType(variableType, variableValue, ref dirty);

            if (layout)
            {
                EditorGUILayout.BeginHorizontal();
            }

            EditorGUI.BeginChangeCheck();

            var list = getConstInputOptions?.Invoke();
            if (list != null)
            {
                variableValue = DisplayListSelector(variableType, rect, content, variableValue, layout, list);
            }
            else if(typeof(Object).IsAssignableFrom(variableType))
            {
                variableValue = DisplayObjectEditor(rect, content, (Object)variableValue, variableType, layout);
            }
            else if(typeof(IUdonEventReceiver).IsAssignableFrom(variableType))
            {
                variableValue = (IUdonEventReceiver)DisplayObjectEditor(rect, content, (Object)variableValue, typeof(UdonBehaviour), layout);
            }
            else if(variableType == typeof(string))
            {
                variableValue = DisplayStringEditor(rect, content, (string) variableValue, layout);
            }
            else if(variableType == typeof(char))
            {
                variableValue = DisplayCharEditor(rect, content, (char) variableValue, layout);
            }
            else if(variableType == typeof(float))
            {
                variableValue = DisplayFloatEditor(rect, content, (float?) variableValue ?? default, layout);
            }
            else if (variableType == typeof(double))
            {
                variableValue = DisplayDoubleEditor(rect, content, (double?) variableValue ?? default, layout);
            }
            else if(variableType == typeof(byte))
            {
                variableValue = (byte)DisplayIntEditor(rect, content, (byte?)variableValue ?? default, layout);
            }
            else if(variableType == typeof(sbyte))
            {
                variableValue = (sbyte)DisplayIntEditor(rect, content, (sbyte?)variableValue ?? default, layout);
            }
            else if(variableType == typeof(int))
            {
                variableValue = DisplayIntEditor(rect, content, (int?)variableValue ?? default, layout);
            }
            else if(variableType == typeof(uint))
            {
                variableValue = (uint)DisplayLongEditor(rect, content, (uint?)variableValue ?? default, layout);
            }
            else if(variableType == typeof(short))
            {
                variableValue = (short)DisplayIntEditor(rect, content, (short?)variableValue ?? default, layout);
            }
            else if(variableType == typeof(ushort))
            {
                variableValue = (ushort)DisplayIntEditor(rect, content, (ushort?)variableValue ?? default, layout);
            }
            else if(variableType == typeof(long))
            {
                variableValue = DisplayLongEditor(rect, content, (long?)variableValue ?? default, layout);
            }
            else if(variableType == typeof(ulong))
            {
                variableValue = DisplayULongEditor(rect, content, (ulong?)variableValue ?? default, layout);
            }
            else if(variableType == typeof(bool))
            {
                variableValue = DisplayBoolEditor(rect, content, (bool?) variableValue ?? default, layout);
            }
            else if(variableType == typeof(Vector2))
            {
                variableValue = DisplayVector2Editor(rect, content, (Vector2?)variableValue ?? default, layout);
            }
            else if(variableType == typeof(Vector2Int))
            {
                variableValue = DisplayVector2IntEditor(rect, content, (Vector2Int?)variableValue ?? default, layout);
            }
            else if(variableType == typeof(Vector3))
            {
                variableValue = DisplayVector3Editor(rect, content, (Vector3?)variableValue ?? default, layout);
            }
            else if(variableType == typeof(Vector3Int))
            {
                variableValue = DisplayVector3IntEditor(rect, content, (Vector3Int?)variableValue ?? default, layout);
            }
            else if(variableType == typeof(Vector4))
            {
                variableValue = DisplayVector4Editor(rect, content, (Vector4?)variableValue ?? default, layout);
            }
            else if(variableType == typeof(Matrix4x4))
            {
                variableValue = DisplayMatrix4X4Editor(rect, content, (Matrix4x4?)variableValue ?? default, layout);
            }
            else if(variableType == typeof(Bounds))
            {
                variableValue = DisplayBoundsEditor(rect, content, (Bounds?)variableValue ?? default, layout);
            }
            else if(variableType == typeof(Ray))
            {
                variableValue = DisplayRayEditor(rect, content, (Ray?)variableValue ?? default, layout);
            }
            else if(variableType == typeof(Rect))
            {
                variableValue = DisplayRectEditor(rect, content, (Rect?)variableValue ?? default, layout);
            }
            else if(variableType == typeof(Plane))
            {
                variableValue = DisplayPlaneEditor(rect, content, (Plane?)variableValue ?? default, layout);
            }
            else if(variableType == typeof(Quaternion))
            {
                variableValue = DisplayQuaternionEditor(rect, content, (Quaternion?)variableValue ?? default, layout);
            }
            else if(variableType == typeof(Color))
            {
                variableValue = DisplayColorEditor(rect, content, (Color?)variableValue ?? default, layout);
            }
            else if(variableType == typeof(Color32))
            {
                variableValue = DisplayColor32Editor(rect, content, (Color32?)variableValue ?? default, layout);
            }
            else if(variableType == typeof(ParticleSystem.MinMaxCurve))
            {
                variableValue = DisplayMinMaxCurveEditor(rect, content,
                    (ParticleSystem.MinMaxCurve?) variableValue ?? default, layout);
            }
            else if (variableType == typeof(AnimationCurve))
            {
                variableValue = DisplayCurveEditor(rect, content, (AnimationCurve)variableValue, layout);
            }
            else if(variableType.IsEnum)
            {
                variableValue = DisplayEnumEditor(rect, content, (Enum)variableValue, variableType, layout);
            }
            else if(variableType == typeof(Type))
            {
                // TODO display proper editor based on what values are exposed
                DisplayTypeEditor(rect, content, (Type) variableValue, layout);
            }
            else if (variableType == typeof(VRCUrl))
            {
                variableValue = DisplayVrcUrlEditor(rect, content, (VRCUrl)variableValue, layout);
            }
            else if (variableType == typeof(LayerMask))
            {
                variableValue = DisplayLayerMaskEditor(rect, content, (LayerMask?) variableValue ?? default, layout);
            }
            else if (variableType == typeof(Gradient))
            {
                variableValue = DisplayGradientEditor(rect, content, (Gradient) variableValue, layout);
            }
            else if (variableType == typeof(VRCPlayerApi))
            {
                variableValue = DisplayPlayerEditor(rect, content, (VRCPlayerApi)variableValue, layout);
            }
            // TODO add more types here
            else
            {
                DisplayMissingEditor(rect, content, variableValue, variableType, layout);
            }

            if (layout)
            {
                EditorGUILayout.EndHorizontal();
            }
            
            
            if(EditorGUI.EndChangeCheck())
            {
                dirty = true;
            }

            return variableValue;
        }

        public static object DisplayArrayPropertyEditor(
            GUIContent variableName, 
            Type variableType, 
            object variableValue, 
            ref bool dirty, 
            ref bool showList, 
            ref ReorderableList list,
            bool layout = true,
            Rect rect = default)
        {
            if (!variableType.IsArray)
            {
                Debug.LogWarning("Trying to display a non array type using the array method!");
                return variableValue;
            }

            Type elementType = variableType.GetElementType();

            if (variableValue == null)
            {
                variableValue = Array.CreateInstance(elementType, 0);
                dirty = true;
            }

            return DisplayArrayPropertyEditor(
                elementType, 
                variableValue as Array, 
                ref dirty,
                ref showList, 
                variableName,
                ref list,
                layout,
                rect);
        }

        #region TypeEditors

        private static void DisplayMissingEditor(Rect rect, GUIContent symbol, object value, Type variableType, bool layout)
        {
            string display = "null";
            if (value != null)
            {
                display = value.ToString();
            }
            GUIContent content = new GUIContent(display);
            //GUIContent content = new GUIContent("No defined editor for type of " + variableType);
            DisplayLabel(rect, content, layout);
        }

        private static void DisplayTypeEditor(Rect rect, GUIContent symbol, Type typeValue, bool layout)
        {
            GUIContent content = new GUIContent(
                typeValue == null ? 
                    "Type = null" :
                    $"Type = {CyanTriggerNameHelpers.GetTypeFriendlyName(typeValue)}");
            DisplayLabel(rect, content, layout);
        }

        private static void DisplayLabel(Rect rect, GUIContent content, bool layout)
        {
            if (layout)
            {
                EditorGUILayout.LabelField(content);
            }
            else
            {
                EditorGUI.LabelField(rect, content);
            }
        }
        
        private static Object DisplayObjectEditor(
            Rect rect, 
            GUIContent symbol, 
            Object unityEngineObjectValue, 
            Type variableType, 
            bool layout)
        {
            if (layout)
            {
                unityEngineObjectValue = EditorGUILayout.ObjectField(symbol, unityEngineObjectValue, variableType, true);
            }
            else
            {
                unityEngineObjectValue = EditorGUI.ObjectField(rect, symbol, unityEngineObjectValue, variableType, true);
            }

            return unityEngineObjectValue;
        }
        
        private static string DisplayStringEditor(Rect rect, GUIContent symbol, string stringValue, bool layout)
        {
            if (layout)
            {
                stringValue = EditorGUILayout.TextField(symbol, stringValue);
            }
            else
            {
                stringValue = EditorGUI.TextField(rect, symbol, stringValue);
            }

            return stringValue;
        }
        
        private static char DisplayCharEditor(Rect rect, GUIContent symbol, char charValue, bool layout)
        {
            string val = charValue.ToString();
            if (layout)
            {
                val = EditorGUILayout.TextField(symbol, val);
            }
            else
            {
                val = EditorGUI.TextField(rect, symbol, val);
            }

            if (string.IsNullOrEmpty(val))
            {
                return '\0';
            }
            
            return val[val.Length-1];
        }
        
        private static bool DisplayBoolEditor(Rect rect, GUIContent symbol, bool boolValue, bool layout)
        {
            if (layout)
            {
                boolValue = EditorGUILayout.Toggle(symbol, boolValue);
            }
            else
            {
                rect.xMin += 1;
                Rect toggleRect = new Rect(rect);
                float toggleWidth = GUI.skin.label.CalcSize(symbol).x + 15;
                toggleRect.width = toggleWidth;
                boolValue = EditorGUI.Toggle(toggleRect, symbol, boolValue);

                if (!EditorGUI.showMixedValue)
                {
                    Rect labelRect = new Rect(rect);
                    labelRect.width = rect.width - toggleWidth;
                    labelRect.x += toggleWidth;
                    EditorGUI.LabelField(labelRect, boolValue.ToString());
                }
            }
            
            return boolValue;
        }
        
        private static int DisplayIntEditor(Rect rect, GUIContent symbol, int intValue, bool layout)
        {
            if (layout)
            {
                intValue = EditorGUILayout.IntField(symbol, intValue);
            }
            else
            {
                intValue = EditorGUI.IntField(rect, symbol, intValue);
            }

            return intValue;
        }
        
        private static long DisplayLongEditor(Rect rect, GUIContent symbol, long longValue, bool layout)
        {
            if (layout)
            {
                longValue = EditorGUILayout.LongField(symbol, longValue);
            }
            else
            {
                longValue = EditorGUI.LongField(rect, symbol, longValue);
            }

            return longValue;
        }
        
        private static ulong DisplayULongEditor(Rect rect, GUIContent symbol, ulong ulongValue, bool layout)
        {
            long longValue = Convert.ToInt64(long.MaxValue & ulongValue);
            if (layout)
            {
                longValue = EditorGUILayout.LongField(symbol, longValue);
            }
            else
            {
                longValue = EditorGUI.LongField(rect, symbol, longValue);
            }

            return Convert.ToUInt64((longValue < 0 ? long.MaxValue+longValue : longValue));
        }
        
        private static float DisplayFloatEditor(Rect rect, GUIContent symbol, float floatValue, bool layout)
        {
            if (layout)
            {
                floatValue = EditorGUILayout.FloatField(symbol, floatValue);
            }
            else
            {
                floatValue = EditorGUI.FloatField(rect, symbol, floatValue);
            }

            return floatValue;
        }
        
        private static double DisplayDoubleEditor(Rect rect, GUIContent symbol, double doubleValue, bool layout)
        {
            if (layout)
            {
                doubleValue = EditorGUILayout.DoubleField(symbol, doubleValue);
            }
            else
            {
                doubleValue = EditorGUI.DoubleField(rect, symbol, doubleValue);
            }

            return doubleValue;
        }
        
        private static Vector2 DisplayVector2Editor(Rect rect, GUIContent symbol, Vector2 vector2Value, bool layout)
        {
            if (layout)
            {
                vector2Value = EditorGUILayout.Vector2Field(symbol, vector2Value);
            }
            else
            {
                vector2Value = EditorGUI.Vector2Field(rect, symbol, vector2Value);
            }

            return vector2Value;
        }
        
        private static Vector2Int DisplayVector2IntEditor(Rect rect, GUIContent symbol, Vector2Int vector2Value, bool layout)
        {
            if (layout)
            {
                vector2Value = EditorGUILayout.Vector2IntField(symbol, vector2Value);
            }
            else
            {
                vector2Value = EditorGUI.Vector2IntField(rect, symbol, vector2Value);
            }

            return vector2Value;
        }
        
        private static Vector3 DisplayVector3Editor(Rect rect, GUIContent symbol, Vector3 vector3Value, bool layout)
        {
            if (layout)
            {
                vector3Value = EditorGUILayout.Vector3Field(symbol, vector3Value);
            }
            else
            {
                vector3Value = EditorGUI.Vector3Field(rect, symbol, vector3Value);
            }

            return vector3Value;
        }
        
        private static Vector3Int DisplayVector3IntEditor(Rect rect, GUIContent symbol, Vector3Int vector3Value, bool layout)
        {
            if (layout)
            {
                vector3Value = EditorGUILayout.Vector3IntField(symbol, vector3Value);
            }
            else
            {
                vector3Value = EditorGUI.Vector3IntField(rect, symbol, vector3Value);
            }

            return vector3Value;
        }

        private static Vector4 DisplayVector4Editor(Rect rect, GUIContent symbol, Vector4 vector4Value, bool layout)
        {
            if (layout)
            {
                vector4Value = EditorGUILayout.Vector4Field(symbol, vector4Value);
            }
            else
            {
                vector4Value = EditorGUI.Vector4Field(rect, symbol, vector4Value);
            }

            return vector4Value;
        }

        private static readonly float[] Matrix4Rows = new float[4];
        private static readonly GUIContent[][] Matrix4RowLabels =
        {
            new[] { new GUIContent(" 0"), new GUIContent(" 1"), new GUIContent(" 2"), new GUIContent(" 3") },
            new[] { new GUIContent(" 4"), new GUIContent(" 5"), new GUIContent(" 6"), new GUIContent(" 7") },
            new[] { new GUIContent(" 8"), new GUIContent(" 9"), new GUIContent("10"), new GUIContent("11") },
            new[] { new GUIContent("12"), new GUIContent("13"), new GUIContent("14"), new GUIContent("14") },
        };
        private static Matrix4x4 DisplayMatrix4X4Editor(Rect rect, GUIContent symbol, Matrix4x4 matrixValue, bool layout)
        {
            if (layout)
            {
                float tempLabelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 16;

                for (int row = 0; row < 4; ++row)
                {
                    EditorGUILayout.BeginHorizontal();

                    for (int col = 0; col < 4; ++col)
                    {
                        int index = row * 4 + col;
                        matrixValue[index] = EditorGUILayout.FloatField(index.ToString(), matrixValue[index]);
                    }
                    
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUIUtility.labelWidth = tempLabelWidth;
            }
            else
            {
                float height = rect.height * 0.25f;
                for (int row = 0; row < 4; ++row)
                {
                    Rect rowRect = new Rect(
                        rect.x,
                        rect.y + height * row,
                        rect.width,
                        height
                    );
                    
                    int rowInd = row * 4;
                    Matrix4Rows[0] = matrixValue[row + 0];
                    Matrix4Rows[1] = matrixValue[row + 4];
                    Matrix4Rows[2] = matrixValue[row + 8];
                    Matrix4Rows[3] = matrixValue[row + 12];
                    
                    EditorGUI.BeginChangeCheck();
                    EditorGUI.MultiFloatField(rowRect, Matrix4RowLabels[row], Matrix4Rows);
                    if (EditorGUI.EndChangeCheck())
                    {
                        matrixValue[row + 0] = Matrix4Rows[0];
                        matrixValue[row + 4] = Matrix4Rows[1];
                        matrixValue[row + 8] = Matrix4Rows[2];
                        matrixValue[row + 12] = Matrix4Rows[3];
                    }
                }
            }

            return matrixValue;
        }
        
        private static Bounds DisplayBoundsEditor(Rect rect, GUIContent symbol, Bounds bounds, bool layout)
        {
            float tempLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 60;
            
            rect.height *= 0.5f;
            bounds.center = DisplayVector3Editor(rect, new GUIContent("Center"), bounds.center, layout);
            rect.y += rect.height;
            bounds.size = DisplayVector3Editor(rect, new GUIContent("Size"), bounds.size, layout);

            EditorGUIUtility.labelWidth = tempLabelWidth;
            return bounds;
        }
        
        private static Ray DisplayRayEditor(Rect rect, GUIContent symbol, Ray ray, bool layout)
        {
            float tempLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 60;

            Vector3 origin = ray.origin;
            Vector3 direction = ray.direction;
            
            EditorGUI.BeginChangeCheck();
            
            rect.height *= 0.5f;
            origin = DisplayVector3Editor(rect, new GUIContent("Origin"), origin, layout);
            rect.y += rect.height;
            direction = DisplayVector3Editor(rect, new GUIContent("Direction"), direction, layout);
           
            if (EditorGUI.EndChangeCheck())
            {
                ray = new Ray(origin, direction);
            }

            EditorGUIUtility.labelWidth = tempLabelWidth;
            return ray;
        }
        
        private static Rect DisplayRectEditor(Rect rect, GUIContent symbol, Rect rectValue, bool layout)
        {
            float tempLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 60;

            rect.height *= 0.5f;
            rectValue.position = DisplayVector2Editor(rect, new GUIContent("Position"), rectValue.position, layout);
            rect.y += rect.height;
            rectValue.size = DisplayVector2Editor(rect, new GUIContent("Size"), rectValue.size, layout);

            EditorGUIUtility.labelWidth = tempLabelWidth;
            return rectValue;
        }
        
        private static Plane DisplayPlaneEditor(Rect rect, GUIContent symbol, Plane plane, bool layout)
        {
            float tempLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 60;

            Vector3 normal = plane.normal;
            float distance = plane.distance;
            
            EditorGUI.BeginChangeCheck();
            
            rect.height *= 0.5f;
            normal = DisplayVector3Editor(rect, new GUIContent("Normal"), normal, layout);
            rect.y += rect.height;
            distance = DisplayFloatEditor(rect, new GUIContent("Distance"), distance, layout);
            
            if (EditorGUI.EndChangeCheck())
            {
                plane = new Plane(normal, distance);
            }

            EditorGUIUtility.labelWidth = tempLabelWidth;
            return plane;
        }
        

        private static Vector3 _cachedQuaternionEulerVector;
        private static Quaternion DisplayQuaternionEditor(Rect rect, GUIContent symbol, Quaternion quaternionValue, bool layout)
        {
            int controlIndex = GUIUtility.GetControlID(FocusType.Passive);

            // Vector3 property editors create 4 controls. Check if current control is within this range.
            bool IsCurrentControl()
            {
                return controlIndex < GUIUtility.keyboardControl && 
                       GUIUtility.keyboardControl <= controlIndex + 4;
            }

            Vector3 euler = quaternionValue.eulerAngles;
            if (IsCurrentControl())
            {
                euler = _cachedQuaternionEulerVector;
            }

            euler = DisplayVector3Editor(rect, symbol, euler, layout);
            quaternionValue = Quaternion.Euler(euler);
            
            if (IsCurrentControl())
            {
                _cachedQuaternionEulerVector = euler;
            }
            
            return quaternionValue;
            /*
            Vector4 quaternionVector4 = new Vector4(quaternionValue.x, quaternionValue.y, quaternionValue.z, quaternionValue.w);
            quaternionVector4 = DisplayVector4Editor(rect, symbol, quaternionVector4, layout);
            return new Quaternion(quaternionVector4.x, quaternionVector4.y, quaternionVector4.z, quaternionVector4.w);
            */
        }

        private static Color DisplayColorEditor(Rect rect,  GUIContent symbol, Color colorValue, bool layout)
        {
            if (layout)
            {
                colorValue = EditorGUILayout.ColorField(symbol, colorValue);
            }
            else
            {
                colorValue = EditorGUI.ColorField(rect, symbol, colorValue);
            }

            return colorValue;
        }

        private static Color32 DisplayColor32Editor(Rect rect, GUIContent symbol, Color32 colorValue, bool layout)
        {
            return DisplayColorEditor(rect, symbol, colorValue, layout);
        }

        private static Enum DisplayEnumEditor(Rect rect, GUIContent symbol, Enum enumValue, Type enumType, bool layout)
        {
            if (enumValue == null)
            {
                enumValue = (Enum)Enum.ToObject(enumType, 0);
            }
            
            if (layout)
            {
                enumValue = EditorGUILayout.EnumPopup(symbol, enumValue);
            }
            else
            {
                enumValue = EditorGUI.EnumPopup(rect, symbol, enumValue);
            }

            return enumValue;
        }

        private static VRCUrl DisplayVrcUrlEditor(Rect rect, GUIContent symbol, VRCUrl urlValue, bool layout)
        {
            if (urlValue == null)
            {
                urlValue = new VRCUrl("");
            }
            return new VRCUrl(DisplayStringEditor(rect, symbol, urlValue.Get(), layout));
        }

        private static ParticleSystem.MinMaxCurve DisplayMinMaxCurveEditor(Rect rect, GUIContent symbol,
            ParticleSystem.MinMaxCurve minMaxCurve, bool layout)
        {
            float tempLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 70;

            float multiplier = minMaxCurve.curveMultiplier;
            AnimationCurve minCurve = minMaxCurve.curveMin ?? new AnimationCurve();
            AnimationCurve maxCurve = minMaxCurve.curveMax ?? new AnimationCurve();
            if (layout)
            {
                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField(symbol);
                EditorGUI.indentLevel++;
                multiplier = EditorGUILayout.FloatField("Multiplier", multiplier);
                minCurve = EditorGUILayout.CurveField("Min Curve", minCurve);
                maxCurve = EditorGUILayout.CurveField("Max Curve", maxCurve);
                EditorGUI.indentLevel--;
                EditorGUILayout.EndVertical();
            }
            else
            {
                float height = (rect.height - 4) / 3;
                Rect r1 = new Rect(rect);
                r1.height = height;
                Rect r2 = new Rect(r1);
                r2.y = r1.yMax + 2;
                Rect r3 = new Rect(r2);
                r3.y = r2.yMax + 2;
                
                //EditorGUI.LabelField(rect, symbol); // TODO?
                multiplier = EditorGUI.FloatField(r1, "Multiplier", multiplier);
                minCurve = EditorGUI.CurveField(r2, "Min Curve", minCurve);
                maxCurve = EditorGUI.CurveField(r3, "Max Curve", maxCurve);
            }

            EditorGUIUtility.labelWidth = tempLabelWidth;
            
            return new ParticleSystem.MinMaxCurve(multiplier, minCurve, maxCurve);
        }

        private static AnimationCurve DisplayCurveEditor(Rect rect, GUIContent symbol, AnimationCurve curve,
            bool layout)
        {
            if (curve == null)
            {
                curve = new AnimationCurve();
            }
            
            if (layout)
            {
                return EditorGUILayout.CurveField(symbol, curve);
            }

            return EditorGUI.CurveField(rect, symbol, curve);
        }

        private static LayerMask DisplayLayerMaskEditor(Rect rect, GUIContent symbol, LayerMask maskValue, bool layout)
        {
            // Using workaround from http://answers.unity.com/answers/1387522/view.html
            if (layout)
            {
                EditorGUILayout.LabelField(symbol);
                LayerMask tempMask = EditorGUILayout.MaskField(InternalEditorUtility.LayerMaskToConcatenatedLayersMask(maskValue), InternalEditorUtility.layers);
                maskValue = InternalEditorUtility.ConcatenatedLayersMaskToLayerMask(tempMask);
            }
            else
            {
                LayerMask tempMask = EditorGUI.MaskField(rect, InternalEditorUtility.LayerMaskToConcatenatedLayersMask(maskValue), InternalEditorUtility.layers);
                maskValue = InternalEditorUtility.ConcatenatedLayersMaskToLayerMask(tempMask);
            }
                
            return maskValue;
        }
        
        // TODO Will this ever be used?
        private static LayerMask DisplayLayerEditor(Rect rect, GUIContent symbol, int layer, bool layout)
        {
            if (layout)
            {
                return EditorGUILayout.LayerField(symbol, layer);
            }
            return EditorGUI.LayerField(rect, symbol, layer);
        }

        private static Gradient DisplayGradientEditor(Rect rect, GUIContent symbol, Gradient gradient, bool layout)
        {
            if (layout)
            {
                return EditorGUILayout.GradientField(symbol, gradient);    
            }
            return EditorGUI.GradientField(rect, symbol, gradient);
        }

        private static VRCPlayerApi DisplayPlayerEditor(Rect rect, GUIContent symbol, VRCPlayerApi player, bool layout)
        {
            if (!EditorApplication.isPlaying)
            {
                DisplayMissingEditor(rect, symbol, player, typeof(VRCPlayerApi), layout);
                return player;
            }

            List<(GUIContent, object)> playerList = new List<(GUIContent, object)>();
            foreach (var curPlayer in VRCPlayerApi.AllPlayers)
            {
                playerList.Add((new GUIContent($"({curPlayer.playerId}) {curPlayer.displayName}"), curPlayer));
            }

            int selectedId = player == null ? -1 : VRCPlayerApi.GetPlayerId(player);
            bool FindSelectedPlayer(object other)
            {
                if (!(other is VRCPlayerApi otherPlayer))
                {
                    return false;
                }
                return selectedId == otherPlayer.playerId;
            }
            
            return (VRCPlayerApi)DisplayListSelector(typeof(VRCPlayerApi), rect, symbol, player, layout, playerList, FindSelectedPlayer);
        }

        private static object DisplayListSelector(
            Type type,
            Rect rect,
            GUIContent symbol,
            object data,
            bool layout,
            List<(GUIContent, object)> items,
            Func<object, bool> selectionCompare = null)
        {
            if (selectionCompare == null)
            {
                object compareData = data;
                selectionCompare = ((obj) => (obj == null && compareData == null) || (obj != null && obj.Equals(compareData)));
            }
            
            int curSelected = 0;
            GUIContent[] displayNames = new GUIContent[items.Count + 1];
            displayNames[0] = new GUIContent("-");
            for (int i = 0; i < items.Count; ++i)
            {
                var obj = items[i];
                object curData = obj.Item2;
                if (selectionCompare(curData))
                {
                    curSelected = i + 1;
                }
                displayNames[i + 1] = obj.Item1;
            }

            // Ensure empty selection resets to default value and does not leave previous.
            if (curSelected == 0)
            {
                bool dirty = false;
                object value = ResetToDefaultValue(type, data, ref dirty);
                if (dirty)
                {
                    data = value;
                    GUI.changed = true;
                }
            }

            int selected;
            if (layout)
            {
                selected = EditorGUILayout.Popup(symbol, curSelected, displayNames);
            }
            else
            {
                selected = EditorGUI.Popup(rect, symbol, curSelected, displayNames);
            }
            
            if (selected != curSelected)
            {
                data = selected != 0 ? items[selected-1].Item2 : GetDefaultForType(type);
            }

            return data;
        }

        #endregion

        public static void CreateReorderableListForVariable(
            Type variableType,
            Array variableValue,
            ref ReorderableList list)
        {
            if (list != null)
            {
                return;
            }
            
            ReorderableList listInstance = list = new ReorderableList(
                variableValue, 
                variableType, 
                true, 
                false,
                true,
                true);
            list.headerHeight = 0;
            list.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                bool changed = false;
                listInstance.list[index] = DisplayPropertyEditor(
                    rect,
                    GUIContent.none,
                    variableType,
                    listInstance.list[index],
                    ref changed);
            };
            list.onAddCallback = reorderableList =>
            {
                int length = reorderableList.list.Count;
                Array values = Array.CreateInstance(variableType, length + 1);
                for (int i = 0; i < length; ++i)
                {
                    values.SetValue(reorderableList.list[i], i);
                }

                reorderableList.list = values;
            };
            list.onRemoveCallback = reorderableList =>
            {
                int selected = reorderableList.index;
                int length = reorderableList.list.Count;
                Array values = Array.CreateInstance(variableType, length - 1);

                int selectedFound = 0;
                for (int i = 0; i < values.Length; ++i)
                {
                    if (i == selected)
                    {
                        selectedFound = 1;
                    }
                    values.SetValue(reorderableList.list[i + selectedFound], i);
                }
                reorderableList.list = values;
            };
            list.elementHeight = HeightForInLineEditor(variableType);
        }
        
        public static Array DisplayArrayPropertyEditor(
            Type variableType,
            Array variableValue,
            ref bool dirty,
            ref bool showList,
            GUIContent content,
            ref ReorderableList list,
            bool layout,
            Rect rect)
        {
            if (layout)
            {
                EditorGUILayout.BeginVertical();
            }

            ReorderableList reorderableList = list;
            DrawFoldoutListHeader(
                content,
                ref showList,
                true,
                variableValue.Length,
                size =>
                {
                    if (reorderableList == null)
                    {
                        return;
                    }

                    Array values = Array.CreateInstance(variableType, size);
                    for (int i = 0; i < size; ++i)
                    {
                        values.SetValue(i < reorderableList.list.Count ? reorderableList.list[i] : default, i);
                    }

                    reorderableList.list = values;
                },
                // Only allow drag for GameObject or Component fields
                typeof(GameObject).IsAssignableFrom(variableType) ||
                 typeof(Component).IsAssignableFrom(variableType) ||
                 typeof(IUdonEventReceiver).IsAssignableFrom(variableType),
                dragObjects =>
                {
                    if (reorderableList == null)
                    {
                        return;
                    }
                    
                    List<Object> objects = GetGameObjectsOrComponentsFromDraggedObjects(dragObjects, variableType);

                    if (objects.Count == 0)
                    {
                        return;
                    }
                    
                    int startSize = reorderableList.list.Count;
                    int size = startSize + objects.Count;
                    Array values = Array.CreateInstance(variableType, size);
                    for (int i = 0; i < reorderableList.list.Count; ++i)
                    {
                        values.SetValue(reorderableList.list[i], i);
                    }
                    for (int i = 0; i < objects.Count; ++i)
                    {
                        values.SetValue(objects[i], startSize + i);
                    }

                    reorderableList.list = values;
                },
                false, 
                true, 
                layout,
                rect);

            if (!showList)
            {
                if (layout)
                {
                    EditorGUILayout.EndVertical();
                }
                return variableValue;
            }
            
            if (list == null)
            {
                CreateReorderableListForVariable(variableType, variableValue, ref list);
            }
            
            EditorGUI.BeginChangeCheck();

            if (layout)
            {
                list.DoLayoutList();
            }
            else
            {
                rect.y += FoldoutListHeaderHeight + 2;
                rect.height -= FoldoutListHeaderHeight + 2;
                list.DoList(rect);
            }

            bool allEqual = list.count == variableValue.Length;
            for (int i = 0; allEqual && i < list.count && i < variableValue.Length; ++i)
            {
                allEqual &= list.list[i] != variableValue.GetValue(i);
            }
        
            if (
                EditorGUI.EndChangeCheck() ||
                list.count != variableValue.Length ||
                !allEqual
            )
            {
                if (variableValue.Length != list.count)
                {
                    variableValue = Array.CreateInstance(variableType, list.count);
                }

                for (int i = 0; i < variableValue.Length; ++i)
                {
                    variableValue.SetValue(list.list[i], i);
                }
                
                dirty = true;
            }
            
            if (layout)
            {
                EditorGUILayout.EndVertical();
            }

            return variableValue;
        }

        public static List<Object> GetGameObjectsOrComponentsFromDraggedObjects(Object[] dragObjects, Type type)
        {
            List<Object> objects = new List<Object>();
            bool isGameObject = typeof(GameObject).IsAssignableFrom(type);
            bool isComponent = typeof(Component).IsAssignableFrom(type) ||
                               typeof(IUdonEventReceiver).IsAssignableFrom(type);
                    
            for (int i = 0; i < dragObjects.Length; ++i)
            {
                var obj = dragObjects[i];
                if (isGameObject)
                {
                    if (obj is GameObject gameObject)
                    {
                        objects.Add(gameObject);
                    }
                    else if (obj is Component component)
                    {
                        objects.Add(component.gameObject);
                    }
                }
                else if (isComponent)
                {
                    if (obj is Component component)
                    {
                        objects.Add(component);
                    }
                    else if (obj is GameObject gameObject)
                    {
                        var components = gameObject.GetComponents(type);
                        if (components.Length > 0)
                        {
                            objects.AddRange(components);
                        }
                    }
                }
            }

            return objects;
        }
        
        public static void DrawFoldoutListHeader(
            GUIContent content,
            ref bool visibilityState,
            bool showSizeEditor,
            int currentSize,
            Action<int> onSizeChanged,
            bool allowItemDrag,
            Action<Object[]> onItemDragged,
            bool showError = false,
            bool showHeaderBackground = true,
            bool layout = true,
            Rect rect = default,
            string documentationTooltip = null,
            string documentationLink = null)
        {
            Rect foldMainRect = rect;
            if (layout)
            {
                foldMainRect = EditorGUILayout.BeginHorizontal();
            }
            
            Rect foldoutRect = new Rect(foldMainRect.x + 17, foldMainRect.y + 1, foldMainRect.width - 18, FoldoutListHeaderHeight);
            Rect header = new Rect(foldMainRect);
            header.height = foldoutRect.height + 4;
            if (showHeaderBackground && Event.current.type == EventType.Repaint)
            {
                if (_headerBackgroundStyle == null)
                {
                    _headerBackgroundStyle = "RL Header";
                }
                _headerBackgroundStyle.Draw(header, false, false, false, false);
            }
            
            Rect sizeRect = new Rect(foldoutRect);
            float separatorSize = 6;
            float maxSizeWidth = 75;

            bool showDocumentationButtons =
                !string.IsNullOrEmpty(documentationTooltip) && !string.IsNullOrEmpty(documentationLink);
            if (showDocumentationButtons)
            {
                foldoutRect.width -= 20;
            }
            
            if (visibilityState && showSizeEditor)
            {
                foldoutRect.width -= maxSizeWidth - separatorSize;
            }

            if (showError)
            {
                content.image = EditorGUIUtility.FindTexture("Error");
            }
            
            // Check dragged objects before foldout as it will become "used" after
            Event evt = Event.current;
            if (allowItemDrag &&
                visibilityState && 
                header.Contains(evt.mousePosition))
            {
                if (evt.type == EventType.DragUpdated)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                }
                if (evt.type == EventType.DragPerform)
                {
                    Object[] dragObjects = DragAndDrop.objectReferences.ToArray();
                    onItemDragged?.Invoke(dragObjects);
                    DragAndDrop.AcceptDrag();
                    evt.Use();
                }
            }
            
            CyanTriggerNameHelpers.TruncateContent(content, foldoutRect);
            bool show = EditorGUI.Foldout(foldoutRect, visibilityState, content, true);
            // Just clicked the arrow, unfocus any elements, which could have been the size component
            if (!show && visibilityState)
            {
                GUI.FocusControl(null);
            }
            visibilityState = show;

            if (visibilityState && showSizeEditor)
            {
                sizeRect.y += 1;
                sizeRect.height -= 1;
                sizeRect.width = maxSizeWidth;
                sizeRect.x += foldoutRect.width - separatorSize;

                float prevWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 30;
                
                int size = EditorGUI.IntField(sizeRect, "Size", currentSize);
                size = Math.Max(0, size);
                
                EditorGUIUtility.labelWidth = prevWidth;

                if (size != currentSize)
                {
                    onSizeChanged?.Invoke(size);
                }
            }
            
            if (showDocumentationButtons)
            {
                Rect docRect = new Rect(foldoutRect.xMax + 4, foldoutRect.y + 1, 16, 16);
                CyanTriggerEditorUtils.DrawDocumentationButton(docRect, documentationTooltip, documentationLink);
            }
            
            if (layout)
            {
                EditorGUILayout.EndHorizontal();
                float offset = 0;
#if !UNITY_2019_4_OR_NEWER
                offset = -3;
#endif
                GUILayout.Space(header.height + offset);
            }
        }

        public static void DrawButtonFooter(
            GUIContent[] icons, 
            Action[] buttons, 
            bool[] shouldDisable, 
            string documentationTooltip = null,
            string documentationLink = null)
        {
            if (_footerButtonStyle == null)
            {
                _footerButtonStyle = "RL FooterButton";
                _footerBackgroundStyle = "RL Footer";
            }
            
            
            Rect footerRect = EditorGUILayout.BeginHorizontal();
            float xMax = footerRect.xMax;
#if UNITY_2019_4_OR_NEWER
            xMax -= 8;
            footerRect.height = 16;
#else
            footerRect.height = 11;
#endif
            float x = xMax - 8f;
            const float buttonWidth = 25;
            x -= buttonWidth * icons.Length;
            footerRect = new Rect(x, footerRect.y, xMax - x, footerRect.height);
                    
            if (Event.current.type == EventType.Repaint)
            {
                _footerBackgroundStyle.Draw(footerRect, false, false, false, false);
            }

#if !UNITY_2019_4_OR_NEWER
            footerRect.y -= 3f;
#endif
            
            for (int i = 0; i < icons.Length; ++i)
            {
                Rect buttonRect = new Rect(x + 4f + buttonWidth * i, footerRect.y, buttonWidth, 13f);
                
                EditorGUI.BeginDisabledGroup(shouldDisable[i]);
                
                GUIStyle style = _footerButtonStyle;
                if (icons[i].image == null)
                {
                    style = new GUIStyle { alignment = TextAnchor.LowerCenter, fontSize = 8};
                    style.normal.textColor = GUI.skin.label.normal.textColor;
                }
                if (GUI.Button(buttonRect, icons[i], style))
                {
                    buttons[i]?.Invoke();
                }
                EditorGUI.EndDisabledGroup();
            }
                    
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(documentationTooltip) && !string.IsNullOrEmpty(documentationLink))
            {
                Rect docRect = new Rect(x - 20, footerRect.y, 16, 16);
                CyanTriggerEditorUtils.DrawDocumentationButton(docRect, documentationTooltip, documentationLink);
            }
            
            GUILayout.Space(footerRect.height + 4);
        }

        public static Rect DrawErrorIcon(Rect rect, string reason)
        {
            GUIContent errorIcon = EditorGUIUtility.TrIconContent("CollabError", reason);
            Rect errorRect = new Rect(rect);
            float iconWidth = 15;
            float spaceBetween = 1;
            errorRect.width = iconWidth;
            errorRect.y += 3;
                
            EditorGUI.LabelField(errorRect, errorIcon);
                
            rect.x += iconWidth + spaceBetween;
            rect.width -= iconWidth + spaceBetween;

            return rect;
        }

        public static void InitializeMultiInputEditors(
            CyanTriggerActionInstanceRenderData actionInstanceRenderData,
            Func<Type, List<CyanTriggerEditorVariableOption>> getVariableOptionsForType)
        {
            var actionProperty = actionInstanceRenderData.Property;
            var variableDefinitions = actionInstanceRenderData.VariableDefinitions;

            // No variables, no need to initialize anything.
            if (variableDefinitions.Length == 0)
            {
                return;
            }
            
            CyanTriggerActionVariableDefinition variableDefinition = variableDefinitions[0];
            
            // Not a multi-editor, no need to initialize.
            if ((variableDefinition.variableType & CyanTriggerActionVariableTypeDefinition.AllowsMultiple) == 0)
            {
                return;
            }
            
            var multiInputListProperty = 
                actionProperty.FindPropertyRelative(nameof(CyanTriggerActionInstance.multiInput));
                    
            CreateActionVariableInstanceMultiInputEditor(
                actionInstanceRenderData, 
                0, 
                multiInputListProperty,
                variableDefinition, 
                getVariableOptionsForType);
        }

        public static float GetHeightForActionInstanceInputEditors(
            CyanTriggerActionInstanceRenderData actionInstanceRenderData,
            bool checkCustomHeight = true)
        {
            var actionProperty = actionInstanceRenderData.Property;
            var inputListProperty = actionProperty.FindPropertyRelative(nameof(CyanTriggerActionInstance.inputs));

            var variableDefinitions = actionInstanceRenderData.VariableDefinitions;
            if (inputListProperty.arraySize != variableDefinitions.Length)
            {
                Debug.LogWarning($"Improper variable input size! {inputListProperty.arraySize} != {variableDefinitions.Length}");
                inputListProperty.arraySize = variableDefinitions.Length;
            }
            
            // Custom Height Implementation
            if (checkCustomHeight
                && CyanTriggerCustomNodeInspectorManager.Instance.TryGetCustomInspector(
                    actionInstanceRenderData.ActionInfo,
                    out var customNodeInspector)
                && customNodeInspector.HasCustomHeight(actionInstanceRenderData))
            {
                return customNodeInspector.GetHeightForInspector(actionInstanceRenderData);
            }
            
            float height = 0;
            int visibleCount = 0;
            for (int curInput = 0; curInput < variableDefinitions.Length; ++curInput)
            {
                CyanTriggerActionVariableDefinition variableDefinition = variableDefinitions[curInput];
                if (variableDefinition == null)
                {
                    continue;
                }

                if ((variableDefinition.variableType & CyanTriggerActionVariableTypeDefinition.Hidden) != 0)
                {
                    continue;
                }
                ++visibleCount;
                
                // First option is a multi input editor
                if (curInput == 0 &&
                    (variableDefinition.variableType & CyanTriggerActionVariableTypeDefinition.AllowsMultiple) != 0)
                {
                    height += GetHeightForActionVariableInstanceMultiInputEditor(
                        variableDefinition.type.Type,
                        actionInstanceRenderData.ExpandedInputs[curInput],
                        actionInstanceRenderData.InputLists[curInput]);
                }
                else
                {
                    SerializedProperty inputProperty = inputListProperty.GetArrayElementAtIndex(curInput);
                    height += GetHeightForActionVariableInstanceInputEditor(
                        variableDefinition,
                        inputProperty,
                        actionInstanceRenderData.ExpandedInputs[curInput],
                        ref actionInstanceRenderData.InputLists[curInput]);
                }
            }

            return height + Mathf.Max(0, 5 * (visibleCount + 1));
        }

        public static float GetHeightForActionVariableInstanceMultiInputEditor(
            Type propertyType,
            bool expandList,
            ReorderableList list)
        {
            if (!expandList)
            {
                return FoldoutListHeaderAreaHeight;
            }
            
            bool displayEditorInLine = TypeHasInLineEditor(propertyType);
            return FoldoutListHeaderAreaHeight
                   + list.GetHeight()
                   + (displayEditorInLine ? 0 : EditorGUIUtility.singleLineHeight + 5);
        }

        private static float GetHeightForActionVariableInstanceInputEditor(
            CyanTriggerActionVariableDefinition variableDefinition,
            SerializedProperty inputProperty,
            bool expandList,
            ref ReorderableList list)
        {
            SerializedProperty isVariableProperty =
                inputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.isVariable));
            bool isVariable = isVariableProperty.boolValue;
            float height = GetHeightForActionInputInLineEditor(variableDefinition, isVariable);
            
            Type propertyType = variableDefinition.type.Type;
            // input based array editors are dependent on the size of the array.
            if (propertyType.IsArray && !isVariable)
            {
                SerializedProperty dataProperty =
                    inputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.data));
                var data = CyanTriggerSerializableObject.ObjectFromSerializedProperty(dataProperty);
                
                // Initialize type if missing
                bool dirty = false;
                object updatedValue = CreateInitialValueForType(propertyType, data, ref dirty);
                if (dirty)
                {
                    data = updatedValue;
                    CyanTriggerSerializableObject.UpdateSerializedProperty(dataProperty, data);
                }
                
                height += 5;
                height += HeightForEditor(
                    propertyType,
                    data,
                    expandList,
                    ref list);
            }

            return height;
        }

        public static void DrawActionInstanceInputEditors(
            CyanTriggerActionInstanceRenderData actionInstanceRenderData,
            Func<Type, List<CyanTriggerEditorVariableOption>> getVariableOptionsForType,
            Rect rect = default,
            bool layout = false,
            Action<HashSet<string>, HashSet<string>> onVariableDeleted = null)
        {
            actionInstanceRenderData.ContainsNull = false;
            
            var actionProperty = actionInstanceRenderData.Property;
            var variableDefinitions = actionInstanceRenderData.VariableDefinitions;
            var inputListProperty = actionProperty.FindPropertyRelative(nameof(CyanTriggerActionInstance.inputs));

            if (inputListProperty.arraySize != variableDefinitions.Length)
            {
                inputListProperty.arraySize = variableDefinitions.Length;
            }
            
            if (actionInstanceRenderData.ExpandedInputs.Length != variableDefinitions.Length)
            {
                actionInstanceRenderData.UpdateVariableSize();
            }
            
            // Draw custom inspectors
            if (CyanTriggerCustomNodeInspectorManager.Instance.TryGetCustomInspector(
                    actionInstanceRenderData.ActionInfo,
                    out var customNodeInspector))
            {
                customNodeInspector.RenderInspector(
                    actionInstanceRenderData, 
                    variableDefinitions, 
                    getVariableOptionsForType, 
                    rect, 
                    layout);
                return;
            }

            bool shouldCheckOutputVariables = onVariableDeleted != null;
            HashSet<string> removedVariables = new HashSet<string>();
            
            for (int curInput = 0; curInput < variableDefinitions.Length; ++curInput)
            {
                CyanTriggerActionVariableDefinition variableDefinition = variableDefinitions[curInput];
                
                Rect inputRect = new Rect(rect);
                
                // First option is a multi input editor
                if (curInput == 0 &&
                    (variableDefinition.variableType & CyanTriggerActionVariableTypeDefinition.AllowsMultiple) != 0)
                {
                    var multiInputListProperty = 
                        actionProperty.FindPropertyRelative(nameof(CyanTriggerActionInstance.multiInput));
                    
                    // TODO check for array of output variables if any are removed.
                    
                    DrawActionVariableInstanceMultiInputEditor(
                        actionInstanceRenderData,
                        curInput,
                        multiInputListProperty, 
                        variableDefinition,
                        getVariableOptionsForType, 
                        ref inputRect,
                        layout);
                }
                else
                {
                    SerializedProperty inputProperty = inputListProperty.GetArrayElementAtIndex(curInput);
                    
                    // Get old variable guid to know if a new output variable is removed.
                    SerializedProperty guidProp = null;
                    string oldGuid = null;
                    bool isOutput = (variableDefinition.variableType &
                                     CyanTriggerActionVariableTypeDefinition.VariableOutput) != 0;
                    if (shouldCheckOutputVariables && isOutput)
                    {
                        SerializedProperty nameProp = inputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.name));
                        guidProp = inputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.variableID));
                        
                        string varName = nameProp.stringValue;
                        oldGuid = guidProp.stringValue;
                        
                        // We know we have a valid output variable set here
                        isOutput = string.IsNullOrEmpty(varName) && !string.IsNullOrEmpty(oldGuid);
                    }
                    
                    
                    DrawActionVariableInstanceInputEditor(
                        actionInstanceRenderData,
                        curInput,
                        inputProperty, 
                        variableDefinition,
                        getVariableOptionsForType, 
                        ref inputRect,
                        layout);
                    
                    
                    // Old guid does not match new guid. Output variable was removed.
                    if (shouldCheckOutputVariables && isOutput && oldGuid != guidProp.stringValue)
                    {
                        removedVariables.Add(oldGuid);
                    }
                }

                // Only update rect size when variable is not hidden.
                if ((variableDefinition.variableType & CyanTriggerActionVariableTypeDefinition.Hidden) == 0)
                {
                    rect.y += inputRect.height + 5;
                    rect.height -= inputRect.height + 5;
                }
            }

            if (shouldCheckOutputVariables && removedVariables.Count > 0)
            {
                onVariableDeleted(removedVariables, new HashSet<string>());
            }
        }

        public static void DrawActionVariableInstanceInputEditor(
            CyanTriggerActionInstanceRenderData actionInstanceRenderData,
            int inputIndex,
            SerializedProperty variableProperty,
            CyanTriggerActionVariableDefinition variableDefinition,
            Func<Type, List<CyanTriggerEditorVariableOption>> getVariableOptionsForType,
            ref Rect rect,
            bool layout = false,
            Func<List<(GUIContent, object)>> getConstInputOptions = null)
        {
            if (variableDefinition == null)
            {
                return;
            }
            Type propertyType = variableDefinition.type.Type;

            GUIContent variableDisplayName =
                new GUIContent(variableDefinition.displayName, variableDefinition.description);
            
            rect.height = GetHeightForActionVariableInstanceInputEditor(
                variableDefinition,
                variableProperty,
                actionInstanceRenderData.ExpandedInputs[inputIndex],
                ref actionInstanceRenderData.InputLists[inputIndex]);
            
            Rect inputRect = new Rect(rect);
            
            // Skip hidden input, but set default value
            if ((variableDefinition.variableType & CyanTriggerActionVariableTypeDefinition.Hidden) != 0)
            {
                SerializedProperty dataProperty =
                    variableProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.data));
                
                // For hidden variables, assume that this is creating a new variable with the specified name and a new guid.
                if ((variableDefinition.variableType & CyanTriggerActionVariableTypeDefinition.VariableOutput) != 0)
                {
                    // TODO create helper method for creating variables and reuse here and in general out property editors
                    SerializedProperty idProperty =
                        variableProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.variableID));
                    if (string.IsNullOrEmpty(idProperty.stringValue))
                    {
                        idProperty.stringValue = Guid.NewGuid().ToString();
                    }
                    CyanTriggerSerializableObject.UpdateSerializedProperty(dataProperty, variableDefinition.displayName);
                }
                // Not a new variable, just use default value.
                else
                {
                    CyanTriggerSerializableObject.UpdateSerializedProperty(dataProperty, variableDefinition.defaultValue?.Obj);
                }
                
                return;
            }

            if (layout)
            {
                EditorGUILayout.Space();
                inputRect = EditorGUILayout.BeginHorizontal(GUILayout.Height(EditorGUIUtility.singleLineHeight));
                EditorGUILayout.Space();
            }

            SerializedProperty isVariableProperty =
                variableProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.isVariable));

            bool isVariable = isVariableProperty.boolValue;
            RenderActionInputInLine(
                variableDefinition,
                variableProperty,
                getVariableOptionsForType,
                getConstInputOptions,
                inputRect,
                true,
                variableDisplayName,
                true,
                GUIContent.none,
                ref actionInstanceRenderData.NeedsVerify,
                actionInstanceRenderData.AllowsUnityObjectConstants);

            if (isVariable != isVariableProperty.boolValue)
            {
                actionInstanceRenderData.NeedsRedraws = true;
            }

            actionInstanceRenderData.ContainsNull |= InputContainsNullVariableOrValue(variableProperty);
            
            if (layout)
            {
                EditorGUILayout.EndHorizontal();
            }

            // TODO handle other multiline editor types
            if (!actionInstanceRenderData.NeedsRedraws &&
                propertyType.IsArray && 
                !isVariableProperty.boolValue) // Or is a type that is multiline editor
            {
                SerializedProperty dataProperty =
                    variableProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.data));
                
                inputRect.y += EditorGUIUtility.singleLineHeight + 5;
                inputRect.height = rect.height - EditorGUIUtility.singleLineHeight;

                bool prevShow = actionInstanceRenderData.ExpandedInputs[inputIndex];

                int size = 0;
                // On first creation, this only can be null?
                // TODO figure out why
                if (actionInstanceRenderData.InputLists != null)
                {
                    size = actionInstanceRenderData.InputLists[inputIndex]?.count ?? 0;
                }


                GUIContent content =
                    new GUIContent(variableDefinition.displayName, variableDefinition.description);
                DrawArrayEditor(
                    dataProperty,
                    content,
                    propertyType,
                    ref actionInstanceRenderData.ExpandedInputs[inputIndex],
                    ref actionInstanceRenderData.InputLists[inputIndex], 
                    layout, 
                    inputRect);

                if (prevShow != actionInstanceRenderData.ExpandedInputs[inputIndex] ||
                    size != (actionInstanceRenderData.InputLists[inputIndex]?.count ?? 0))
                {
                    actionInstanceRenderData.NeedsRedraws = true;
                }
            }
        }

        private static void CreateActionVariableInstanceMultiInputEditor(
            CyanTriggerActionInstanceRenderData actionInstanceRenderData,
            int inputIndex,
            SerializedProperty variableProperty,
            CyanTriggerActionVariableDefinition variableDefinition,
            Func<Type, List<CyanTriggerEditorVariableOption>> getVariableOptionsForType)
        {
            if (actionInstanceRenderData.InputLists[inputIndex] != null)
            {
                return;
            }
            
            Type propertyType = variableDefinition.type.Type;
            bool displayEditorInLine = TypeHasInLineEditor(propertyType);
            
            ReorderableList list = new ReorderableList(
                variableProperty.serializedObject, 
                variableProperty, 
                true, 
                false, 
                true, 
                true);
            list.headerHeight = 0;
            list.drawElementCallback = (elementRect, index, isActive, isFocused) =>
            {
                SerializedProperty property = variableProperty.GetArrayElementAtIndex(index);
                RenderActionInputInLine(
                    variableDefinition,
                    property,
                    getVariableOptionsForType,
                    null,
                    elementRect,
                    false,
                    GUIContent.none,
                    displayEditorInLine,
                    new GUIContent("Select to Edit"),
                    ref actionInstanceRenderData.NeedsVerify,
                    actionInstanceRenderData.AllowsUnityObjectConstants);
            };
            list.elementHeight = HeightForInLineEditor(propertyType);

            actionInstanceRenderData.InputLists[inputIndex] = list;
        }
        
        public static void DrawActionVariableInstanceMultiInputEditor(
            CyanTriggerActionInstanceRenderData actionInstanceRenderData,
            int inputIndex,
            SerializedProperty variableProperty,
            CyanTriggerActionVariableDefinition variableDefinition,
            Func<Type, List<CyanTriggerEditorVariableOption>> getVariableOptionsForType,
            ref Rect rect,
            bool layout = false)
        {
            CreateActionVariableInstanceMultiInputEditor(
                actionInstanceRenderData, 
                inputIndex, 
                variableProperty,
                variableDefinition, 
                getVariableOptionsForType);

            if (layout)
            {
                EditorGUILayout.Space();
            }

            Type propertyType = variableDefinition.type.Type;
            bool displayEditorInLine = TypeHasInLineEditor(propertyType);
            
            rect.height =
                GetHeightForActionVariableInstanceMultiInputEditor(
                    propertyType,
                    actionInstanceRenderData.ExpandedInputs[inputIndex],
                    actionInstanceRenderData.InputLists[inputIndex]);
            Rect inputRect = new Rect(rect);
            inputRect.height = FoldoutListHeaderAreaHeight;

            GUIContent variableDisplayName =
                new GUIContent(variableDefinition.displayName, variableDefinition.description);

            bool prevExpand = actionInstanceRenderData.ExpandedInputs[inputIndex];
            int arraySize = variableProperty.arraySize;
            
            DrawFoldoutListHeader(
                variableDisplayName,
                ref actionInstanceRenderData.ExpandedInputs[inputIndex],
                true,
                variableProperty.arraySize,
                size =>
                {
                    int prevSize = variableProperty.arraySize;
                    variableProperty.arraySize = size;

                    for (int i = prevSize; i < size; ++i)
                    {
                        SerializedProperty property = variableProperty.GetArrayElementAtIndex(i);
                        SerializedProperty dataProperty =
                            property.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.data));
                        CyanTriggerSerializableObject.UpdateSerializedProperty(dataProperty, null);
                    }
                },
                // Only allow drag for GameObject or Component fields
                typeof(GameObject).IsAssignableFrom(propertyType) ||
                typeof(Component).IsAssignableFrom(propertyType) ||
                typeof(IUdonEventReceiver).IsAssignableFrom(propertyType),
                dragObjects =>
                {
                    List<Object> objects = GetGameObjectsOrComponentsFromDraggedObjects(dragObjects, propertyType);

                    int startIndex = variableProperty.arraySize;
                    variableProperty.arraySize += objects.Count;
                    for (int i = 0; i < objects.Count; ++i)
                    {
                        SerializedProperty property = variableProperty.GetArrayElementAtIndex(startIndex + i);
                        SerializedProperty isVarProperty =
                            property.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.isVariable));
                        isVarProperty.boolValue = false;
                        SerializedProperty dataProperty =
                            property.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.data));
                        
                        CyanTriggerSerializableObject.UpdateSerializedProperty(dataProperty, objects[i]);
                    }
                    variableProperty.serializedObject.ApplyModifiedProperties();
                },
                false,
                true,
                layout,
                inputRect);

            if (actionInstanceRenderData.ExpandedInputs[inputIndex])
            {
                if (layout)
                {
                    GUILayout.Space(2);
                    actionInstanceRenderData.InputLists[inputIndex].DoLayoutList();
                }
                else
                {
                    inputRect.y += FoldoutListHeaderAreaHeight;
                    actionInstanceRenderData.InputLists[inputIndex].DoList(inputRect);
                }
            }
            
            if (prevExpand != actionInstanceRenderData.ExpandedInputs[inputIndex] ||
                arraySize != variableProperty.arraySize)
            {
                arraySize = variableProperty.arraySize;
                actionInstanceRenderData.NeedsRedraws = true;
            }

            actionInstanceRenderData.ContainsNull |= arraySize == 0;
            for (int curInput = 0; curInput < arraySize && !actionInstanceRenderData.ContainsNull; ++curInput)
            {
                var inputProp = variableProperty.GetArrayElementAtIndex(curInput);
                actionInstanceRenderData.ContainsNull |= InputContainsNullVariableOrValue(inputProp);
            }

            // TODO figure out how to get the list here.
            if (!displayEditorInLine && actionInstanceRenderData.InputLists[inputIndex].index != -1)
            {
                EditorGUILayout.LabelField($"Selected item {actionInstanceRenderData.InputLists[inputIndex].index}");
            }
        }

        private static float GetHeightForActionInputInLineEditor(
            CyanTriggerActionVariableDefinition variableDefinition,
            bool isVariable)
        {
            if ((variableDefinition.variableType & CyanTriggerActionVariableTypeDefinition.Hidden) != 0)
            {
                return 0;
            }
            bool allowsCustomValues =
                (variableDefinition.variableType & CyanTriggerActionVariableTypeDefinition.Constant) != 0;
            bool allowsVariables =
                (variableDefinition.variableType & CyanTriggerActionVariableTypeDefinition.VariableInput) != 0;
            
            if (!allowsCustomValues && !allowsVariables)
            {
                return 0;
            }
            
            bool allowsOutput =
                (variableDefinition.variableType & CyanTriggerActionVariableTypeDefinition.VariableOutput) != 0;
            if (allowsOutput)
            {
                return EditorGUIUtility.singleLineHeight;
            }

            var type = variableDefinition.type.Type;

            // Heights for input arrays will be calculated else where
            if (type.IsArray)
            {
                return EditorGUIUtility.singleLineHeight;
            }

            if (isVariable)
            {
                return EditorGUIUtility.singleLineHeight;
            }
            
            return HeightForInLineEditor(type);
        }
        
        
        private static void RenderActionInputInLine(
            CyanTriggerActionVariableDefinition variableDefinition,
            SerializedProperty variableProperty,
            Func<Type, List<CyanTriggerEditorVariableOption>> getVariableOptionsForType,
            Func<List<(GUIContent, object)>> getConstInputOptions,
            Rect rect,
            bool displayLabel,
            GUIContent labelContent,
            bool displayEditor,
            GUIContent editorLabelContent,
            ref bool needsVerify,
            bool allowsUnityObjectConstants)
        {
            SerializedProperty dataProperty =
                variableProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.data));

            // Skip hidden input, but set default value
            if ((variableDefinition.variableType & CyanTriggerActionVariableTypeDefinition.Hidden) != 0)
            {
                // TODO verify this works?
                CyanTriggerSerializableObject.UpdateSerializedProperty(dataProperty, variableDefinition.defaultValue?.Obj);
                return;
            }

            bool allowsCustomValues =
                (variableDefinition.variableType & CyanTriggerActionVariableTypeDefinition.Constant) != 0;
            bool allowsVariables =
                (variableDefinition.variableType & CyanTriggerActionVariableTypeDefinition.VariableInput) != 0;
            bool outputVar = 
                (variableDefinition.variableType & CyanTriggerActionVariableTypeDefinition.VariableOutput) != 0;
            
            Type propertyType = variableDefinition.type.Type;
            
            // If is AssetCyanTrigger and type is Unity Object,
            // do not allow direct constants as unity can't serialize it properly
            if (!allowsUnityObjectConstants && (typeof(Object).IsAssignableFrom(propertyType) 
                                                || typeof(IUdonEventReceiver).IsAssignableFrom(propertyType)))
            {
                allowsCustomValues = false;
            }

            // TODO verify this isn't possible. What
            if (!allowsCustomValues && !allowsVariables)
            {
                return;
            }

            SerializedProperty isVariableProperty =
                variableProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.isVariable));
            SerializedProperty idProperty =
                variableProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.variableID));
            SerializedProperty nameProperty =
                variableProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.name));

            bool isVariable = isVariableProperty.boolValue;


            float spaceBetween = 5;
            float width = (rect.width - spaceBetween * 2) / 3f;
            Rect labelRect = new Rect(rect.x, rect.y, width, Mathf.Min(rect.height, EditorGUIUtility.singleLineHeight));
            Rect inputRectFull = new Rect(labelRect.xMax + spaceBetween, rect.y, width * 2 + spaceBetween, rect.height);
            Rect typeRect = new Rect(labelRect.xMax + spaceBetween, rect.y, Mathf.Min(width * 0.5f, 65f), rect.height);
            Rect inputRect = new Rect(typeRect.xMax + spaceBetween, rect.y, width * 2 - typeRect.width, rect.height);

            // TODO verify variable value and show error if not valid
            //if (!CyanTriggerUtil.IsValidActionVariableInstance(variableProperty))
            
            // TODO do this properly
            // var valid = variableInstance.IsValid();
            // if (valid != CyanTriggerUtil.InvalidReason.Valid)
            // {
            //     labelRect = CyanTriggerPropertyEditor.DrawErrorIcon(labelRect, valid.ToString());
            // }
            
            if (displayLabel)
            {
                string propertyTypeFriendlyName = CyanTriggerNameHelpers.GetTypeFriendlyName(propertyType);
                if (string.IsNullOrEmpty(labelContent.text))
                {
                    labelContent.text = $"{(outputVar? "out " : "")}{propertyTypeFriendlyName}";
                }

                string updatedTooltip = $"{labelContent.text} ({propertyTypeFriendlyName})";
                if (outputVar)
                {
                    updatedTooltip = $"{updatedTooltip} - The contents of this variable will be modified.";
                }
                if (!string.IsNullOrEmpty(labelContent.tooltip))
                {
                    updatedTooltip += $"\n{labelContent.tooltip}";
                }
                labelContent.tooltip = updatedTooltip;
                
                // TODO show indicator if variable will be edited
                EditorGUI.LabelField(labelRect, labelContent);
            }
            else
            {
                inputRectFull.x -= labelRect.width;
                typeRect.x -= labelRect.width;
                inputRect.x -= labelRect.width;

                inputRectFull.width += labelRect.width;
                inputRect.width += labelRect.width;
            }

            Rect customRect = inputRectFull;
            if (allowsCustomValues && allowsVariables)
            {
                Rect popupRect = typeRect;
                if (!isVariable && propertyType.IsArray && displayEditor)
                {
                    popupRect = inputRectFull;
                }
                popupRect.height = EditorGUIUtility.singleLineHeight;

                string[] options = {"Input", "Variable"};
                EditorGUI.BeginProperty(popupRect, GUIContent.none, isVariableProperty);
                isVariable = isVariableProperty.boolValue = 1 == EditorGUI.Popup(popupRect, isVariable ? 1 : 0, options);
                EditorGUI.EndProperty();
                customRect = inputRect;
            }
            else if (allowsCustomValues)
            {
                isVariable = isVariableProperty.boolValue = false;
            }
            else
            {
                isVariable = isVariableProperty.boolValue = true;
            }
            
            if (isVariable)
            {
                int selected = 0;
                List<string> options = new List<string>();
                List<CyanTriggerEditorVariableOption> varOptions = getVariableOptionsForType(propertyType);
                List<CyanTriggerEditorVariableOption> visibleOptions = new List<CyanTriggerEditorVariableOption>();

                // Check if the variable type is output only.
                bool createNewVar = outputVar && !allowsCustomValues;
                options.Add(createNewVar ? "+New" : "None");

                string idValue = idProperty.stringValue;
                bool isEmpty = string.IsNullOrEmpty(idValue);
                string nameValue = nameProperty.stringValue;
                
                // Go through and add all variable options, checking for which is the current selected item.
                foreach (var varOption in varOptions)
                {
                    // Skip readonly variables for output var options
                    if (outputVar && varOption.IsReadOnly)
                    {
                        continue;
                    }
                    
                    if (idValue == varOption.ID || (isEmpty && nameValue == varOption.Name))
                    {
                        selected = options.Count;
                    }
                    visibleOptions.Add(varOption);

                    string optionName = propertyType != varOption.Type
                        ? $"{varOption.Name} ({CyanTriggerNameHelpers.GetTypeFriendlyName(varOption.Type)})"
                        : varOption.Name;
                    options.Add(optionName);
                }
                
                // TODO add option for new global variable or new local variable which creates the variable before this action
                // Is this needed if outputs are always new?

                
                // When displaying for out variables that do not allow inputs,
                // Add option for new variable and a space for the variable name.
                if (createNewVar && selected == 0)
                {
                    customRect = typeRect;
                    bool dirty = DrawEditor(dataProperty, inputRect, GUIContent.none, typeof(string));

                    if (dirty)
                    {
                        // Sanitize names to prevent weird characters. Note that this is just for display as the actual
                        // variable name will be generated at compile time.
                        string varName = (string)CyanTriggerSerializableObject.ObjectFromSerializedProperty(dataProperty);
                        string sanitizedName = CyanTriggerNameHelpers.SanitizeName(varName);
                        if (!string.IsNullOrEmpty(varName) && varName != sanitizedName)
                        {
                            CyanTriggerSerializableObject.UpdateSerializedProperty(dataProperty, sanitizedName);
                        }
                    }
                    
                    
                    // TODO verify unique names for variable providers. (Is this even the right place for that?)
                }

                int prevSelected = selected;
                EditorGUI.BeginProperty(customRect, GUIContent.none, idProperty);
                EditorGUI.BeginProperty(customRect, GUIContent.none, nameProperty);
                selected = EditorGUI.Popup(customRect, selected, options.ToArray());
                EditorGUI.EndProperty();
                EditorGUI.EndProperty();

                // Swapping between new variable and existing variables should cause a ui redraw to recreate lists and verify data.
                if (createNewVar && selected != prevSelected && (prevSelected == 0 || selected == 0))
                {
                    needsVerify = true;
                }
                
                if (selected == 0)
                {
                    nameProperty.stringValue = "";
                    
                    if (createNewVar)
                    {
                        // TODO move this to better location?
                        if (prevSelected != 0 || string.IsNullOrEmpty(idProperty.stringValue))
                        {
                            idProperty.stringValue = Guid.NewGuid().ToString();
                        }
                    }
                    else
                    {
                        idProperty.stringValue = "";
                    }
                }
                else
                {
                    var varOption = visibleOptions[selected - 1];
                    idProperty.stringValue = varOption.ID;
                    nameProperty.stringValue = varOption.Name;
                }
            }
            else if (!displayEditor)
            {
                EditorGUI.LabelField(customRect, editorLabelContent);
            }
            else if (!propertyType.IsArray)
            {
                // TODO verify unique names for variable providers. (Is this even the right place for that?)
                // Note that variable providers are obsolete and most likely shouldn't be addressed here as this is for general non variable properties
                DrawEditor(dataProperty, customRect, GUIContent.none, propertyType, false, getConstInputOptions);
            }
            else
            {
                // Cannot edit arrays here, please call RenderActionInputArray directly
            }
        }

        public static bool InputContainsNullVariableOrValue(SerializedProperty variableProperty)
        {
            SerializedProperty isVariableProperty =
                variableProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.isVariable));

            if (isVariableProperty.boolValue)
            {
                SerializedProperty idProperty =
                    variableProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.variableID));
                SerializedProperty nameProperty =
                    variableProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.name));

                return string.IsNullOrEmpty(idProperty.stringValue) && string.IsNullOrEmpty(nameProperty.stringValue);
            }
            
            SerializedProperty dataProperty =
                variableProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.data));
            return CyanTriggerSerializableObject.ObjectFromSerializedProperty(dataProperty) == null;
        }
    }
}