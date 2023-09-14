using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using I2.Loc;
using UniLinq;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(LanguageSource))]
public class LocalizationEditor : Editor
{
    private SerializedProperty _onMissingTranslation;
    private Dictionary<string, bool> termFoldouts = new Dictionary<string, bool>();
    private Dictionary<string, bool> titleFoldouts = new Dictionary<string, bool>();
    private Dictionary<string, bool> subtitleFoldouts = new Dictionary<string, bool>();
    private Dictionary<string, bool> manufacturerFoldouts = new Dictionary<string, bool>();
    private Dictionary<string, bool> descriptionFoldouts = new Dictionary<string, bool>();

    private static bool GetOrSetFalseIfNot(IDictionary<string, bool> foldout, string name)
    {
        if (foldout.TryGetValue(name, out var val))
        {
            return val;
        }
        foldout[name] = false;
        return false;
    }
    void OnEnable()
    {
        _onMissingTranslation = serializedObject.FindProperty("OnMissingTranslation");
    }

    private static void DrawHorizontalLine()
    {
        DrawHorizontalLine(Color.gray);
    }

    private static void DrawThinHorizontalLine()
    {
        DrawHorizontalLine(Color.gray, 1);
    }
    
    private static void DrawHorizontalLine(Color color, int thickness = 2, int padding = 4)
    {
        Rect r = EditorGUILayout.GetControlRect(GUILayout.Height(padding + thickness));
        r.height = thickness;
        r.y += padding / 2;
        r.x -= 4;
        r.width += 14;
        EditorGUI.DrawRect(r, color);
    }
    
    private static void DrawVerticalLine()
    {
        DrawVerticalLine(Color.gray);
    }

    private static void DrawThinVerticalLine()
    {
        DrawVerticalLine(Color.gray, 1);
    }
    
    private static void DrawVerticalLine(Color color, int thickness = 2, int padding = 4)
    {
        Rect r = EditorGUILayout.GetControlRect(GUILayout.Width(padding + thickness));
        r.width = thickness;
        r.x += padding / 2;
        r.y -= 4;
        r.height += 14;
        EditorGUI.DrawRect(r, color);
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(_onMissingTranslation);
        serializedObject.ApplyModifiedProperties();
        // Now here we add the language fields
        // Given that the script runs in the editor it should be fine
        var targetObject = serializedObject.targetObject as LanguageSource;
        var targetLanguages = targetObject!.mLanguages;
        var removeAtIndices = new List<int>();
        EditorGUILayout.LabelField("Languages",EditorStyles.boldLabel);
        DrawHorizontalLine();
        for (var i = 0; i < targetLanguages.Count; i++)
        {
            targetLanguages[i].Name = EditorGUILayout.TextField("Name",targetLanguages[i].Name);
            targetLanguages[i].Code = EditorGUILayout.TextField("Code", targetLanguages[i].Code);
            targetLanguages[i].Flags = 0;
            if (GUILayout.Button("Remove Language"))
            {
                removeAtIndices.Add(i);
            }
            DrawThinHorizontalLine();
        }

        for (var i = removeAtIndices.Count - 1; i >= 0; i--)
        {
            targetLanguages.RemoveAt(removeAtIndices[i]);
        }

        if (GUILayout.Button("+"))
        {
            targetLanguages.Add(new LanguageData());
        }
        DrawHorizontalLine();

        //TODO: Specific term editor for part descriptions and such
        //TODO: Spreadsheet based main term editor

        var partsIndices = new Dictionary<string, int>();
        var partsEditors = new
            List<(string Name, (int? Title, int? Subtitle, int? Manufacturer, int? Description) Indices)>();
        var skip = new HashSet<int>();
        var targetTerms = targetObject!.mTerms;
        for (int i = 0; i < targetTerms.Count; i++)
        {
            var term = targetTerms[i];
            if (term.Term.StartsWith("Parts/Title/"))
            {
                var partName = term.Term.Substring("Parts/Title/".Length);
                (int? Title, int? Subtitle, int? Manufacturer, int? Description) val = (null, null, null, null);
                if (partsIndices.TryGetValue(partName, out var index))
                {
                    val = partsEditors[index].Indices;
                    val.Title = i;
                    partsEditors[index] = (partName,val);
                }
                else
                {
                    index = partsEditors.Count;
                    partsIndices[partName] = index;
                    val.Title = i;
                    partsEditors.Add((partName,val));
                }
            }
            if (term.Term.StartsWith("Parts/Subtitle/"))
            {
                var partName = term.Term.Substring("Parts/Subtitle/".Length);
                (int? Title, int? Subtitle, int? Manufacturer, int? Description) val = (null, null, null, null);
                if (partsIndices.TryGetValue(partName, out var index))
                {
                    val = partsEditors[index].Indices;
                    val.Subtitle = i;
                    partsEditors[index] = (partName,val);
                }
                else
                {
                    index = partsEditors.Count;
                    partsIndices[partName] = index;
                    val.Subtitle = i;
                    partsEditors.Add((partName,val));
                }
            }
            if (term.Term.StartsWith("Parts/Manufacturer/"))
            {
                var partName = term.Term.Substring("Parts/Manufacturer/".Length);
                (int? Title, int? Subtitle, int? Manufacturer, int? Description) val = (null, null, null, null);
                if (partsIndices.TryGetValue(partName, out var index))
                {
                    val = partsEditors[index].Indices;
                    val.Manufacturer = i;
                    partsEditors[index] = (partName,val);
                }
                else
                {
                    index = partsEditors.Count;
                    partsIndices[partName] = index;
                    val.Manufacturer = i;
                    partsEditors.Add((partName,val));
                }
            }
            if (term.Term.StartsWith("Parts/Description/"))
            {
                var partName = term.Term.Substring("Parts/Description/".Length);
                (int? Title, int? Subtitle, int? Manufacturer, int? Description) val = (null, null, null, null);
                if (partsIndices.TryGetValue(partName, out var index))
                {
                    val = partsEditors[index].Indices;
                    val.Description = i;
                    partsEditors[index] = (partName,val);
                }
                else
                {
                    index = partsEditors.Count;
                    partsIndices[partName] = index;
                    val.Description = i;
                    partsEditors.Add((partName,val));
                }
            }
        }
        
        removeAtIndices = new List<int>();

        var partIndex = 0;
        foreach (var pair in partsEditors.ToArray())
        {
            if (pair.Indices.Subtitle == null || pair.Indices.Description == null || pair.Indices.Manufacturer == null ||
                pair.Indices.Title == null)
            {
                removeAtIndices.Add(partIndex);
            }
            else
            {
                skip.Add(pair.Indices.Title.Value);
                skip.Add(pair.Indices.Subtitle.Value);
                skip.Add(pair.Indices.Manufacturer.Value);
                skip.Add(pair.Indices.Description.Value);
            }

            partIndex += 1;
        }
        
        for (var i = removeAtIndices.Count - 1; i >= 0; i--)
        {
            partsEditors.RemoveAt(removeAtIndices[i]);
        }

        removeAtIndices = new List<int>();
        EditorGUILayout.LabelField("Terms", EditorStyles.boldLabel);
        DrawHorizontalLine();
        for (var i = 0; i < targetTerms.Count; i++)
        {
            if (skip.Contains(i)) continue;
            targetTerms[i].Term = EditorGUILayout.TextField("Term", targetTerms[i].Term);
            var selection = EditorGUILayout.EnumPopup("Type", targetTerms[i].TermType);
            targetTerms[i].TermType = (eTermType)(selection is eTermType ? selection : eTermType.Text);
            if (termFoldouts[targetTerms[i].Term] =
                EditorGUILayout.Foldout(GetOrSetFalseIfNot(termFoldouts, targetTerms[i].Term), "Localizations"))
            {
                ShowTermEditorFor(targetTerms, i, targetLanguages);
            }

            if (GUILayout.Button("Remove Term"))
            {
                removeAtIndices.Add(i);
            }
            DrawThinHorizontalLine();
        }

        if (GUILayout.Button("Add Term"))
        {
            targetTerms.Add(new TermData());
        }
        DrawHorizontalLine();
        EditorGUILayout.LabelField("Parts", EditorStyles.boldLabel);
        DrawHorizontalLine();

        foreach (var partEditor in partsEditors)
        {
            int title = partEditor.Indices.Title.Value;
            int subtitle = partEditor.Indices.Subtitle.Value;
            int manufacturer = partEditor.Indices.Manufacturer.Value;
            int description = partEditor.Indices.Description.Value;
            EditorGUI.BeginChangeCheck();
            var partName = GUILayout.TextField(partEditor.Name);
            if (EditorGUI.EndChangeCheck())
            {
                targetTerms[title].Term = "Parts/Title/" + partName;
                targetTerms[subtitle].Term = "Parts/Subtitle/" + partName;
                targetTerms[manufacturer].Term = "Parts/Manufacturer/" + partName;
                targetTerms[description].Term = "Parts/Description/" + partName;
            }
            if (titleFoldouts[partName] = EditorGUILayout.Foldout(GetOrSetFalseIfNot(titleFoldouts,partName),"Title"))
            {
                targetTerms[title].TermType = eTermType.Text;
                ShowTermEditorFor(targetTerms, title, targetLanguages);
            }
            if (subtitleFoldouts[partName] = EditorGUILayout.Foldout(GetOrSetFalseIfNot(subtitleFoldouts,partName),"Subtitle"))
            {
                targetTerms[subtitle].TermType = eTermType.Text;
                ShowTermEditorFor(targetTerms, subtitle, targetLanguages);
            }
            if (manufacturerFoldouts[partName] = EditorGUILayout.Foldout(GetOrSetFalseIfNot(manufacturerFoldouts,partName),"Manufacturer"))
            {
                targetTerms[manufacturer].TermType = eTermType.Text;
                ShowTermEditorFor(targetTerms, manufacturer, targetLanguages);
            }
            if (descriptionFoldouts[partName] = EditorGUILayout.Foldout(GetOrSetFalseIfNot(descriptionFoldouts,partName),"Description"))
            {
                targetTerms[description].TermType = eTermType.Text;
                ShowTermEditorFor(targetTerms, description, targetLanguages);
            }

            if (GUILayout.Button("Remove Part"))
            {
                removeAtIndices.Add(title);
                removeAtIndices.Add(subtitle);
                removeAtIndices.Add(manufacturer);
                removeAtIndices.Add(description);
            }
            DrawThinHorizontalLine();
        }
        for (var i = removeAtIndices.Count - 1; i >= 0; i--)
        {
            targetTerms.RemoveAt(removeAtIndices[i]);
        }
        if (GUILayout.Button("Add Part"))
        {
            targetTerms.Add(new TermData()
            {
                Term = "Parts/Title/[NEW PART]",
                TermType = eTermType.Text
            });
            targetTerms.Add(new TermData()
            {
                Term = "Parts/Subtitle/[NEW PART]",
                TermType = eTermType.Text
            });
            targetTerms.Add(new TermData()
            {
                Term = "Parts/Manufacturer/[NEW PART]",
                TermType = eTermType.Text
            });
            targetTerms.Add(new TermData()
            {
                Term = "Parts/Description/[NEW PART]",
                TermType = eTermType.Text
            });
        }
        DrawHorizontalLine();

    }

    private static void ShowTermEditorFor(List<TermData> terms, int index, List<LanguageData> languages)
    {
        if (terms[index].Languages.Length < languages.Count)
        {
            var newLanguages = new string[languages.Count];
            terms[index].Languages.CopyTo(newLanguages, 0);
            for (int j = terms[index].Languages.Length; j < languages.Count; j++)
            {
                newLanguages[j] = "";
            }

            terms[index].Languages = newLanguages;
        }

        for (var j = 0; j < languages.Count; j++)
        {
            terms[index].Languages[j] =
                EditorGUILayout.TextField(languages[j].Name, terms[index].Languages[j]);
        }
    }
}
