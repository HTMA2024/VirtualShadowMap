#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEditor;

namespace EditorTools
{
    /// <summary>
    /// 通用 Console 日志抓取与导出工具
    /// 抓取 Clear 之后的日志，自动 Collapse 去重，支持导出 TXT / CSV
    /// 菜单: Tools/Console Log Capture
    /// </summary>
    public sealed class DiagnosticLogCapture : EditorWindow
    {
        private struct LogEntry
        {
            public string Message;
            public string StackTrace;
            public LogType Type;
            public int Count;
            public string Timestamp;
        }

        private readonly List<LogEntry> mEntries = new List<LogEntry>(256);
        private readonly Dictionary<int, int> mCollapseMap = new Dictionary<int, int>(256);
        private Vector2 mScrollPos;
        private bool mCollapse = true;
        private bool mAutoScroll = true;
        private bool mShowLog = true;
        private bool mShowWarning = true;
        private bool mShowError = true;
        private int mLogCount;
        private int mWarningCount;
        private int mErrorCount;
        private string mSearchFilter = string.Empty;
        private int mSelectedIndex = -1;
        private float mDetailHeight = 150f;

        // 反射缓存
        private static Type sLogEntriesType;
        private static MethodInfo sGetCountMethod;
        private static MethodInfo sGetEntryMethod;
        private static MethodInfo sStartMethod;
        private static MethodInfo sEndMethod;
        private static MethodInfo sClearMethod;
        private static Type sLogEntryType;
        private static FieldInfo sMessageField;
        private static FieldInfo sModeField;

        // GUIStyle 缓存
        private GUIStyle mEvenStyle;
        private GUIStyle mOddStyle;
        private GUIStyle mSelectedStyle;
        private GUIStyle mCountBadgeStyle;
        private GUIStyle mStackTraceStyle;
        private bool mStylesReady;

        [MenuItem("Tools/Console Log Capture")]
        private static void Open()
        {
            var window = GetWindow<DiagnosticLogCapture>("Console Capture");
            window.minSize = new Vector2(400, 300);
            window.Show();
        }

        private void OnEnable()
        {
            InitReflection();
            Application.logMessageReceived += OnLogReceived;
            Refresh();
        }

        private void OnDisable()
        {
            Application.logMessageReceived -= OnLogReceived;
        }

        private void OnLogReceived(string condition, string stackTrace, LogType type)
        {
            AddEntry(condition, stackTrace, type);
            Repaint();
        }

        private static void InitReflection()
        {
            if (sLogEntriesType != null) return;

            var asm = Assembly.GetAssembly(typeof(UnityEditor.Editor));
            sLogEntriesType = asm.GetType("UnityEditor.LogEntries")
                           ?? asm.GetType("UnityEditorInternal.LogEntries");
            if (sLogEntriesType == null) return;

            sGetCountMethod = sLogEntriesType.GetMethod("GetCount",
                BindingFlags.Public | BindingFlags.Static);
            sStartMethod = sLogEntriesType.GetMethod("StartGettingEntries",
                BindingFlags.Public | BindingFlags.Static);
            sEndMethod = sLogEntriesType.GetMethod("EndGettingEntries",
                BindingFlags.Public | BindingFlags.Static);
            sClearMethod = sLogEntriesType.GetMethod("Clear",
                BindingFlags.Public | BindingFlags.Static);
            sGetEntryMethod = sLogEntriesType.GetMethod("GetEntryInternal",
                BindingFlags.Public | BindingFlags.Static);

            sLogEntryType = asm.GetType("UnityEditor.LogEntry")
                         ?? asm.GetType("UnityEditorInternal.LogEntry");
            if (sLogEntryType != null)
            {
                sMessageField = sLogEntryType.GetField("message",
                    BindingFlags.Public | BindingFlags.Instance);
                sModeField = sLogEntryType.GetField("mode",
                    BindingFlags.Public | BindingFlags.Instance);
            }
        }

        private void EnsureStyles()
        {
            if (mStylesReady) return;
            mStylesReady = true;

            mEvenStyle = new GUIStyle("CN EntryBackEven") { richText = true };
            mOddStyle = new GUIStyle("CN EntryBackOdd") { richText = true };
            mSelectedStyle = new GUIStyle("MeTransitionSelect") { richText = true };
            mCountBadgeStyle = new GUIStyle("CN CountBadge")
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 10,
                fixedHeight = 18,
                padding = new RectOffset(4, 4, 0, 0)
            };
            mStackTraceStyle = new GUIStyle(EditorStyles.textArea)
            {
                richText = false, wordWrap = true, fontSize = 11
            };
        }

        private void Refresh()
        {
            mEntries.Clear();
            mCollapseMap.Clear();
            mLogCount = 0;
            mWarningCount = 0;
            mErrorCount = 0;
            mSelectedIndex = -1;

            if (sLogEntriesType == null || sStartMethod == null) return;

            int count = (int)sGetCountMethod.Invoke(null, null);
            sStartMethod.Invoke(null, null);
            var entry = Activator.CreateInstance(sLogEntryType);

            for (int i = 0; i < count; i++)
            {
                sGetEntryMethod.Invoke(null, new object[] { i, entry });
                string msg = (string)sMessageField.GetValue(entry);
                int mode = (int)sModeField.GetValue(entry);
                AddEntry(msg, string.Empty, ModeToLogType(mode));
            }

            sEndMethod.Invoke(null, null);
        }

        private void AddEntry(string message, string stackTrace, LogType type)
        {
            int hash = message.GetHashCode();

            if (mCollapse && mCollapseMap.TryGetValue(hash, out int idx))
            {
                var existing = mEntries[idx];
                existing.Count++;
                mEntries[idx] = existing;
            }
            else
            {
                if (mCollapse)
                    mCollapseMap[hash] = mEntries.Count;
                mEntries.Add(new LogEntry
                {
                    Message = message,
                    StackTrace = stackTrace,
                    Type = type,
                    Count = 1,
                    Timestamp = DateTime.Now.ToString("HH:mm:ss")
                });
            }

            switch (type)
            {
                case LogType.Log:     mLogCount++;     break;
                case LogType.Warning: mWarningCount++; break;
                default:              mErrorCount++;   break;
            }
        }

        private void OnGUI()
        {
            EnsureStyles();
            DrawToolbar();
            DrawLogList();
            if (mSelectedIndex >= 0 && mSelectedIndex < mEntries.Count)
                DrawDetail();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("Clear & Refresh", EditorStyles.toolbarButton, GUILayout.Width(100)))
            {
                sClearMethod?.Invoke(null, null);
                Refresh();
            }
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
                Refresh();

            GUILayout.Space(4);
            mAutoScroll = GUILayout.Toggle(mAutoScroll, "Auto Scroll",
                EditorStyles.toolbarButton, GUILayout.Width(80));

            GUILayout.Space(4);
            bool prevCollapse = mCollapse;
            mCollapse = GUILayout.Toggle(mCollapse, "Collapse",
                EditorStyles.toolbarButton, GUILayout.Width(65));
            if (prevCollapse != mCollapse) Refresh();

            GUILayout.Space(4);
            if (GUILayout.Button("Export ▼", EditorStyles.toolbarDropDown, GUILayout.Width(70)))
            {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("导出为 TXT"), false, () => ExportLogs(false));
                menu.AddItem(new GUIContent("导出为 CSV"), false, () => ExportLogs(true));
                menu.AddItem(new GUIContent("复制全部到剪贴板"), false, CopyAllToClipboard);
                menu.ShowAsContext();
            }

            GUILayout.FlexibleSpace();

            mSearchFilter = EditorGUILayout.TextField(mSearchFilter,
                EditorStyles.toolbarSearchField, GUILayout.Width(200));

            GUILayout.Space(8);
            mShowLog = GUILayout.Toggle(mShowLog, $"Log [{mLogCount}]",
                EditorStyles.toolbarButton, GUILayout.Width(70));
            mShowWarning = GUILayout.Toggle(mShowWarning, $"Warn [{mWarningCount}]",
                EditorStyles.toolbarButton, GUILayout.Width(80));
            mShowError = GUILayout.Toggle(mShowError, $"Err [{mErrorCount}]",
                EditorStyles.toolbarButton, GUILayout.Width(70));

            EditorGUILayout.EndHorizontal();
        }

        private void DrawLogList()
        {
            mScrollPos = EditorGUILayout.BeginScrollView(mScrollPos);

            bool hasFilter = !string.IsNullOrEmpty(mSearchFilter);
            int visibleIdx = 0;

            for (int i = 0, n = mEntries.Count; i < n; i++)
            {
                var entry = mEntries[i];
                if (!ShouldShow(entry.Type)) continue;
                if (hasFilter && entry.Message.IndexOf(mSearchFilter,
                    StringComparison.OrdinalIgnoreCase) < 0) continue;

                bool selected = mSelectedIndex == i;
                GUIStyle rowStyle = selected ? mSelectedStyle
                    : (visibleIdx & 1) == 0 ? mEvenStyle : mOddStyle;

                EditorGUILayout.BeginHorizontal(rowStyle, GUILayout.Height(22));
                GUILayout.Label(GetIcon(entry.Type), GUILayout.Width(20), GUILayout.Height(20));

                string firstLine = GetFirstLine(entry.Message);
                if (GUILayout.Button(firstLine, EditorStyles.label))
                    mSelectedIndex = selected ? -1 : i;

                if (entry.Count > 1)
                    GUILayout.Label(entry.Count.ToString(), mCountBadgeStyle, GUILayout.Width(36));

                EditorGUILayout.EndHorizontal();
                visibleIdx++;
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawDetail()
        {
            var entry = mEntries[mSelectedIndex];

            EditorGUILayout.BeginVertical(GUILayout.Height(mDetailHeight));
            EditorGUILayout.LabelField("详细信息", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.SelectableLabel(entry.Message, mStackTraceStyle,
                GUILayout.MinHeight(40), GUILayout.ExpandHeight(true));

            if (!string.IsNullOrEmpty(entry.StackTrace))
            {
                EditorGUILayout.Space(2);
                EditorGUILayout.SelectableLabel(entry.StackTrace, mStackTraceStyle,
                    GUILayout.MinHeight(40), GUILayout.ExpandHeight(true));
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("复制", GUILayout.Width(60)))
            {
                string text = string.IsNullOrEmpty(entry.StackTrace)
                    ? entry.Message
                    : entry.Message + "\n" + entry.StackTrace;
                EditorGUIUtility.systemCopyBuffer = text;
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        // ─── 导出功能 ───

        private List<LogEntry> GetFilteredEntries()
        {
            var result = new List<LogEntry>();
            bool hasFilter = !string.IsNullOrEmpty(mSearchFilter);

            for (int i = 0, n = mEntries.Count; i < n; i++)
            {
                var entry = mEntries[i];
                if (!ShouldShow(entry.Type)) continue;
                if (hasFilter && entry.Message.IndexOf(mSearchFilter,
                    StringComparison.OrdinalIgnoreCase) < 0) continue;
                result.Add(entry);
            }
            return result;
        }

        private void ExportLogs(bool csv)
        {
            var filtered = GetFilteredEntries();
            if (filtered.Count == 0)
            {
                EditorUtility.DisplayDialog("导出", "没有可导出的日志条目。", "确定");
                return;
            }

            string ext = csv ? "csv" : "txt";
            string path = EditorUtility.SaveFilePanel(
                "导出日志", "", $"console_log_{DateTime.Now:yyyyMMdd_HHmmss}.{ext}", ext);
            if (string.IsNullOrEmpty(path)) return;

            var sb = new StringBuilder(filtered.Count * 128);

            if (csv)
            {
                sb.AppendLine("时间,类型,次数,消息,堆栈");
                for (int i = 0, n = filtered.Count; i < n; i++)
                {
                    var e = filtered[i];
                    sb.Append(e.Timestamp).Append(',');
                    sb.Append(e.Type).Append(',');
                    sb.Append(e.Count).Append(',');
                    sb.Append('"').Append(e.Message.Replace("\"", "\"\"")).Append('"').Append(',');
                    sb.Append('"').Append(e.StackTrace.Replace("\"", "\"\"")).Append('"');
                    sb.AppendLine();
                }
            }
            else
            {
                for (int i = 0, n = filtered.Count; i < n; i++)
                {
                    var e = filtered[i];
                    sb.Append('[').Append(e.Timestamp).Append("] ");
                    sb.Append('[').Append(e.Type).Append(']');
                    if (e.Count > 1) sb.Append(" (x").Append(e.Count).Append(')');
                    sb.Append(' ').AppendLine(e.Message);
                    if (!string.IsNullOrEmpty(e.StackTrace))
                        sb.AppendLine(e.StackTrace);
                    sb.AppendLine();
                }
            }

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            EditorUtility.DisplayDialog("导出成功",
                $"已导出 {filtered.Count} 条日志到:\n{path}", "确定");
        }

        private void CopyAllToClipboard()
        {
            var filtered = GetFilteredEntries();
            if (filtered.Count == 0)
            {
                EditorUtility.DisplayDialog("复制", "没有可复制的日志条目。", "确定");
                return;
            }

            var sb = new StringBuilder(filtered.Count * 128);
            for (int i = 0, n = filtered.Count; i < n; i++)
            {
                var e = filtered[i];
                sb.Append('[').Append(e.Timestamp).Append("] ");
                sb.Append('[').Append(e.Type).Append(']');
                if (e.Count > 1) sb.Append(" (x").Append(e.Count).Append(')');
                sb.Append(' ').AppendLine(e.Message);
                if (!string.IsNullOrEmpty(e.StackTrace))
                    sb.AppendLine(e.StackTrace);
                sb.AppendLine();
            }

            EditorGUIUtility.systemCopyBuffer = sb.ToString();
            Debug.Log($"[DiagnosticLogCapture] 已复制 {filtered.Count} 条日志到剪贴板");
        }

        // ─── 工具方法 ───

        private bool ShouldShow(LogType type)
        {
            switch (type)
            {
                case LogType.Log:     return mShowLog;
                case LogType.Warning: return mShowWarning;
                default:              return mShowError;
            }
        }

        private static LogType ModeToLogType(int mode)
        {
            if ((mode & 0x01) != 0 || (mode & 0x02) != 0) return LogType.Error;
            if ((mode & 0x100) != 0) return LogType.Warning;
            return LogType.Log;
        }

        private static GUIContent GetIcon(LogType type)
        {
            switch (type)
            {
                case LogType.Warning:   return EditorGUIUtility.IconContent("console.warnicon.sml");
                case LogType.Error:
                case LogType.Exception:
                case LogType.Assert:    return EditorGUIUtility.IconContent("console.erroricon.sml");
                default:                return EditorGUIUtility.IconContent("console.infoicon.sml");
            }
        }

        private static string GetFirstLine(string msg)
        {
            if (string.IsNullOrEmpty(msg)) return string.Empty;
            int idx = msg.IndexOf('\n');
            return idx > 0 ? msg.Substring(0, idx) : msg;
        }
    }
}
#endif
