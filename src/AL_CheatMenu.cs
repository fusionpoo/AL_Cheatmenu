// AL_CheatMenu — in-game cheat window for Amanatsu Location (BepInEx 6 IL2CPP).
// Toggle with the configured key (default F7). Edits the live save data: heroine parameters
// (favorability / inclusiveness / proactivity / curiosity levels), late points, energy (Cost),
// H and massage counts, H gauge stages, time of day, tutorial flags.
using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using AL.User;
using Il2CppInterop.Runtime.Injection;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Il2CppInterop.Runtime;

namespace AL_CheatMenu
{
    [BepInPlugin(GUID, PluginName, Version)]
    public class ALCheatMenuPlugin : BasePlugin
    {
        public const string GUID = "fusionpoo.al.cheatmenu";
        public const string PluginName = "AL Cheat Menu";
        public const string Version = "0.2.1";
        internal static ManualLogSource Logger;
        internal static ConfigEntry<KeyCode> ToggleKey;
        internal static ConfigEntry<float> Scale;

        public override void Load()
        {
            Logger = Log;
            ToggleKey = Config.Bind("General", "Toggle key", KeyCode.F7, "Key that opens / closes the cheat window.");
            Scale = Config.Bind("General", "UI scale", 1.0f, new ConfigDescription("Window scale.", new AcceptableValueRange<float>(0.6f, 2.5f)));
            ClassInjector.RegisterTypeInIl2Cpp<CheatMenuBehaviour>();
            var go = new GameObject("AL_CheatMenu"); go.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<CheatMenuBehaviour>();
            Log.LogInfo($"{PluginName} {Version} loaded. Press {ToggleKey.Value} in game.");
        }
    }

    public class CheatMenuBehaviour : MonoBehaviour
    {
        public CheatMenuBehaviour(IntPtr ptr) : base(ptr) { }

        private GameObject _canvasGo; private RectTransform _content; private TextMeshProUGUI _statusText;
        private bool _open; private int _tab; private float _nextRefresh; private string _status = "";
        private static TMP_FontAsset _font;
        private static readonly Color PanelBg = new Color(0.07f, 0.07f, 0.09f, 0.94f), RowBg = new Color(1f, 1f, 1f, 0.06f), BtnBg = new Color(0.22f, 0.24f, 0.30f, 1f), BtnBgActive = new Color(0.30f, 0.45f, 0.70f, 1f);

        private void Update()
        {
            try
            {
                if (Input.GetKeyDown(ALCheatMenuPlugin.ToggleKey.Value)) Toggle();
                if (_open)
                {
                    if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;
                    if (!Cursor.visible) Cursor.visible = true;
                    if (Time.unscaledTime > _nextRefresh) { _nextRefresh = Time.unscaledTime + 1.5f; Rebuild(); }
                }
            }
            catch (Exception e) { ALCheatMenuPlugin.Logger.LogDebug($"update: {e.Message}"); }
        }

        private void Toggle()
        {
            _open = !_open;
            if (_open) { EnsureCanvas(); _canvasGo.SetActive(true); Rebuild(); }
            else if (_canvasGo != null) _canvasGo.SetActive(false);
        }

        private void Say(string s) { _status = s; if (_statusText != null) _statusText.text = s; }

        // ------------------------------------------------------------------ uGUI scaffolding
        private static TMP_FontAsset Font()
        {
            if (_font != null) return _font;
            try { var s = TMP_Settings.defaultFontAsset; if (s != null) { _font = s; return _font; } } catch { }
            try
            {
                var all = Resources.FindObjectsOfTypeAll(Il2CppType.Of<TMP_FontAsset>());
                foreach (var o in all) { var f = o.TryCast<TMP_FontAsset>(); if (f != null) { _font = f; break; } }
            }
            catch { }
            return _font;
        }

        private static RectOffset Pad(int l, int r, int t, int b) { var o = new RectOffset(); o.left = l; o.right = r; o.top = t; o.bottom = b; return o; }

        private static RectTransform Rect(GameObject go) { var rt = go.GetComponent<RectTransform>(); return rt != null ? rt : go.AddComponent<RectTransform>(); }

        private static GameObject Make(string name, Transform parent)
        {
            var go = new GameObject(name); go.layer = 5; Rect(go); go.transform.SetParent(parent, false); return go;
        }

        private static TextMeshProUGUI Text(Transform parent, string text, float size = 15f, float width = -1f, bool bold = false)
        {
            var go = Make("Text", parent); var t = go.AddComponent<TextMeshProUGUI>();
            var f = Font(); if (f != null) t.font = f;
            t.text = text; t.fontSize = size; t.color = Color.white; t.richText = true; t.enableWordWrapping = false; t.overflowMode = TextOverflowModes.Overflow;
            if (bold) t.fontStyle = FontStyles.Bold;
            t.alignment = TextAlignmentOptions.MidlineLeft;
            var le = go.AddComponent<LayoutElement>(); le.preferredHeight = 26f; if (width > 0) { le.preferredWidth = width; le.minWidth = width; } else le.flexibleWidth = 1f;
            return t;
        }

        private static Button Btn(Transform parent, string label, Action onClick, float width = 60f, bool active = false)
        {
            var go = Make("Button", parent); var img = go.AddComponent<Image>(); img.color = active ? BtnBgActive : BtnBg;
            var b = go.AddComponent<Button>(); b.targetGraphic = img;
            var le = go.AddComponent<LayoutElement>(); le.preferredWidth = width; le.minWidth = width; le.preferredHeight = 26f;
            var tgo = Make("Label", go.transform); var rt = Rect(tgo); rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var t = tgo.AddComponent<TextMeshProUGUI>(); var f = Font(); if (f != null) t.font = f;
            t.text = label; t.fontSize = 14f; t.color = Color.white; t.alignment = TextAlignmentOptions.Center; t.enableWordWrapping = false; t.raycastTarget = false;
            b.onClick.AddListener(DelegateSupport.ConvertDelegate<UnityEngine.Events.UnityAction>(new Action(() => { try { onClick(); } catch (Exception e) { ALCheatMenuPlugin.Logger.LogWarning($"cheat action failed: {e}"); } })));
            return b;
        }

        private static Transform Row(Transform parent, bool box = false)
        {
            var go = Make("Row", parent);
            if (box) { var img = go.AddComponent<Image>(); img.color = RowBg; img.raycastTarget = false; }
            var h = go.AddComponent<HorizontalLayoutGroup>(); h.spacing = 6f; h.childForceExpandWidth = false; h.childForceExpandHeight = false; h.childControlWidth = true; h.childControlHeight = true; h.padding = Pad(4, 4, 2, 2); h.childAlignment = TextAnchor.MiddleLeft;
            var le = go.AddComponent<LayoutElement>(); le.preferredHeight = 30f;
            return go.transform;
        }

        private static Transform Column(Transform parent, bool box = false)
        {
            var go = Make("Column", parent);
            if (box) { var img = go.AddComponent<Image>(); img.color = RowBg; img.raycastTarget = false; }
            var v = go.AddComponent<VerticalLayoutGroup>(); v.spacing = 2f; v.childForceExpandWidth = true; v.childForceExpandHeight = false; v.childControlWidth = true; v.childControlHeight = true; v.padding = Pad(6, 6, 4, 4);
            var fit = go.AddComponent<ContentSizeFitter>(); fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return go.transform;
        }

        private void EnsureCanvas()
        {
            if (_canvasGo != null) return;
            if (EventSystem.current == null)
            {
                var es = new GameObject("AL_CheatMenu_EventSystem"); es.AddComponent<EventSystem>(); es.AddComponent<StandaloneInputModule>(); UnityEngine.Object.DontDestroyOnLoad(es);
            }
            _canvasGo = new GameObject("AL_CheatMenu_Canvas"); _canvasGo.layer = 5; UnityEngine.Object.DontDestroyOnLoad(_canvasGo);
            var canvas = _canvasGo.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32000;
            var scaler = _canvasGo.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920f, 1080f); scaler.matchWidthOrHeight = 0.5f;
            _canvasGo.AddComponent<GraphicRaycaster>();

            var panel = Make("Panel", _canvasGo.transform); var prt = Rect(panel);
            float sc = ALCheatMenuPlugin.Scale.Value;
            prt.anchorMin = new Vector2(0f, 1f); prt.anchorMax = new Vector2(0f, 1f); prt.pivot = new Vector2(0f, 1f); prt.anchoredPosition = new Vector2(30f, -30f); prt.sizeDelta = new Vector2(760f, 820f); prt.localScale = new Vector3(sc, sc, 1f);
            var pimg = panel.AddComponent<Image>(); pimg.color = PanelBg;
            var pv = panel.AddComponent<VerticalLayoutGroup>(); pv.spacing = 4f; pv.padding = Pad(10, 10, 8, 8); pv.childForceExpandWidth = true; pv.childForceExpandHeight = false; pv.childControlWidth = true; pv.childControlHeight = true;

            var header = Row(panel.transform);
            Text(header, $"AL Cheat Menu {ALCheatMenuPlugin.Version}   ({ALCheatMenuPlugin.ToggleKey.Value} to close)", 17f, -1f, true);
            Btn(header, "X", Toggle, 34f);

            var tabs = Row(panel.transform); tabs.name = "Tabs";
            var scroll = Make("Scroll", panel.transform); var srt = Rect(scroll);
            var sle = scroll.AddComponent<LayoutElement>(); sle.flexibleHeight = 1f; sle.preferredHeight = 680f;
            var simg = scroll.AddComponent<Image>(); simg.color = new Color(0, 0, 0, 0.25f);
            var sr = scroll.AddComponent<ScrollRect>(); sr.horizontal = false; sr.vertical = true; sr.scrollSensitivity = 30f; sr.movementType = ScrollRect.MovementType.Clamped;
            var viewport = Make("Viewport", scroll.transform); var vrt = Rect(viewport); vrt.anchorMin = Vector2.zero; vrt.anchorMax = Vector2.one; vrt.offsetMin = new Vector2(4, 4); vrt.offsetMax = new Vector2(-4, -4);
            viewport.AddComponent<RectMask2D>(); var vimg = viewport.AddComponent<Image>(); vimg.color = new Color(0, 0, 0, 0.01f);
            var content = Make("Content", viewport.transform); var crt = Rect(content); crt.anchorMin = new Vector2(0f, 1f); crt.anchorMax = new Vector2(1f, 1f); crt.pivot = new Vector2(0.5f, 1f); crt.offsetMin = Vector2.zero; crt.offsetMax = Vector2.zero;
            var cv = content.AddComponent<VerticalLayoutGroup>(); cv.spacing = 4f; cv.childForceExpandWidth = true; cv.childForceExpandHeight = false; cv.childControlWidth = true; cv.childControlHeight = true; cv.padding = Pad(4, 4, 4, 4);
            var cfit = content.AddComponent<ContentSizeFitter>(); cfit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            sr.viewport = vrt; sr.content = crt;
            _content = crt;
            var status = Row(panel.transform); _statusText = Text(status, "", 14f);
            _tabsRow = tabs;
        }
        private Transform _tabsRow;

        private static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(t.GetChild(i).gameObject);
        }

        private void Rebuild()
        {
            if (_content == null) return;
            try
            {
                Clear(_tabsRow);
                string[] names = { "Heroines", "Time & flags", "About" };
                for (int i = 0; i < names.Length; i++) { int idx = i; Btn(_tabsRow, names[i], () => { _tab = idx; Rebuild(); }, 130f, _tab == i); }
                Clear(_content);
                switch (_tab) { case 0: DrawHeroines(); break; case 1: DrawTimeAndFlags(); break; default: DrawAbout(); break; }
                if (_statusText != null) _statusText.text = _status;
            }
            catch (Exception e) { ALCheatMenuPlugin.Logger.LogWarning($"cheat menu build failed: {e}"); }
        }

        // ------------------------------------------------------------------ data access
        private static SaveData Save()
        {
            try { var g = Manager.Game.Instance; return g != null ? g.SaveData : null; }
            catch { return null; }
        }

        private static bool Table(int idx, out int maxLv, out int lvPoint)
        {
            maxLv = 5; lvPoint = 100;
            try
            {
                var pd = AL.PropertyData.Instance; if (pd == null) return false;
                var pp = pd.GetPointParameter(idx); maxLv = pp.MaxLV; lvPoint = pp.LVPoint; return true;
            }
            catch { return false; }
        }

        private static void SetLevel(GameParameter.ParameterInfo p, int idx, int lv)
        {
            if (p == null || p.Pointer == IntPtr.Zero) return;
            Table(idx, out var max, out var per); lv = Mathf.Clamp(lv, 0, max);
            p.LV = lv; p.Point = lv * per; p.IsMaxLv = lv >= max;
        }

        private static string NameOf(ActorData a)
        {
            try
            {
                var hd = a.HumanData;
                if (hd != null && hd.Parameter != null) { var n = hd.Parameter.fullname; if (!string.IsNullOrEmpty(n)) return n; }
            }
            catch { }
            try { return a.CharaFileName ?? "?"; } catch { return "?"; }
        }

        private static void MaxHeroine(NPCData npc)
        {
            GameParameter gp = null;
            try { gp = npc != null ? npc.GameParameter : null; } catch (Exception e) { ALCheatMenuPlugin.Logger.LogWarning($"max: no GameParameter ({e.Message})"); }
            if (gp == null) { ALCheatMenuPlugin.Logger.LogWarning("max: heroine has no GameParameter yet"); return; }
            void Step(string what, Action a) { try { a(); } catch (Exception e) { ALCheatMenuPlugin.Logger.LogWarning($"max: {what} failed: {e.Message}"); } }
            Step("favorability", () => SetLevel(gp.Favorability, 0, 99));
            Step("inclusiveness", () => SetLevel(gp.Inclusiveness, 1, 99));
            Step("proactivity", () => SetLevel(gp.Proactivity, 2, 99));
            Step("curiosity", () => SetLevel(gp.Curiosity, 3, 99));
            Step("cost", () => gp.Cost = 100);
        }

        // ------------------------------------------------------------------ tabs
        private void DrawHeroines()
        {
            var save = Save();
            if (save == null) { Text(_content, "No save data loaded yet (start or load a game)."); return; }
            var list = save.NPCDataList;
            if (list == null || list.Count == 0) { Text(_content, "No heroines in this save."); return; }

            var top = Row(_content);
            Btn(top, "Max everything for all heroines", () => { for (int i = 0; i < list.Count; i++) MaxHeroine(list[i]); Say("All heroines maxed."); Rebuild(); }, 260f);
            Btn(top, "Refill energy (all)", () => { for (int i = 0; i < list.Count; i++) { var gp = list[i].GameParameter; if (gp != null) gp.Cost = 100; } Say("Energy refilled."); Rebuild(); }, 160f);

            for (int i = 0; i < list.Count; i++)
            {
                var npc = list[i]; if (npc == null) continue;
                var gp = npc.GameParameter;
                var col = Column(_content, true);
                var hr = Row(col);
                Text(hr, $"<b>{NameOf(npc)}</b>   (id {npc.UniqueID}, personality {npc.Personality})", 15f);
                Btn(hr, "Max all", () => { MaxHeroine(npc); Say($"{NameOf(npc)} maxed."); Rebuild(); }, 80f);
                if (gp != null)
                {
                    DrawParam(col, "Favorability", gp.Favorability, 0);
                    DrawParam(col, "Inclusiveness", gp.Inclusiveness, 1);
                    DrawParam(col, "Proactivity", gp.Proactivity, 2);
                    DrawParam(col, "Curiosity", gp.Curiosity, 3);
                    var r = Row(col);
                    Text(r, $"Late points: {gp.LatePoint}", 14f, 150f);
                    Btn(r, "-10", () => { gp.LatePoint = Mathf.Max(0, gp.LatePoint - 10); Rebuild(); }, 44f);
                    Btn(r, "+10", () => { gp.LatePoint += 10; Rebuild(); }, 44f);
                    Btn(r, "0", () => { gp.LatePoint = 0; Rebuild(); }, 34f);
                    Text(r, $"Energy: {gp.Cost}", 14f, 110f);
                    Btn(r, "-10", () => { gp.Cost = Mathf.Max(0, gp.Cost - 10); Rebuild(); }, 44f);
                    Btn(r, "+10", () => { gp.Cost += 10; Rebuild(); }, 44f);
                    Btn(r, "100", () => { gp.Cost = 100; Rebuild(); }, 44f);
                }
                var cnt = npc.GameCount;
                if (cnt != null)
                {
                    var r = Row(col);
                    Text(r, $"H count: {cnt.H}", 14f, 120f);
                    Btn(r, "-1", () => { cnt.H = Mathf.Max(0, cnt.H - 1); Rebuild(); }, 38f);
                    Btn(r, "+1", () => { cnt.H += 1; Rebuild(); }, 38f);
                    Btn(r, "+10", () => { cnt.H += 10; Rebuild(); }, 44f);
                    Text(r, $"Massage: {cnt.Massage}", 14f, 120f);
                    Btn(r, "-1", () => { cnt.Massage = Mathf.Max(0, cnt.Massage - 1); Rebuild(); }, 38f);
                    Btn(r, "+1", () => { cnt.Massage += 1; Rebuild(); }, 38f);
                }
                var hp = npc.HParameter;
                if (hp != null)
                {
                    try
                    {
                        var st = hp.GaugeStage;
                        if (st != null && st.Length > 0)
                        {
                            var r = Row(col);
                            Text(r, "H gauge stages:", 14f, 120f);
                            for (int k = 0; k < st.Length; k++)
                            {
                                int kk = k; Text(r, $"[{k}] {st[k]}", 14f, 56f);
                                Btn(r, "+", () => { try { hp.RankUp(kk); } catch { var a = hp.GaugeStage; a[kk] = a[kk] + 1; } Rebuild(); }, 28f);
                            }
                            Btn(r, "reset", () => { var a = hp.GaugeStage; for (int k2 = 0; k2 < a.Length; k2++) a[k2] = 0; Rebuild(); }, 56f);
                        }
                    }
                    catch (Exception e) { Text(col, $"H gauge: {e.Message}", 13f); }
                }
            }
        }

        private void DrawParam(Transform col, string label, GameParameter.ParameterInfo p, int idx)
        {
            if (p == null) return;
            Table(idx, out var maxLv, out var per);
            var r = Row(col);
            Text(r, $"{label}: Lv {p.LV}/{maxLv}{(p.IsMaxLv ? " (max)" : "")}, {p.Point} pt", 14f, 270f);
            Btn(r, "-", () => { SetLevel(p, idx, p.LV - 1); Rebuild(); }, 30f);
            Btn(r, "+", () => { SetLevel(p, idx, p.LV + 1); Rebuild(); }, 30f);
            Btn(r, "Max", () => { SetLevel(p, idx, maxLv); Rebuild(); }, 50f);
            Btn(r, $"+{per} pt", () => { p.Point += per; Rebuild(); }, 70f);
            Btn(r, "Reset", () => { try { p.Reset(); } catch { SetLevel(p, idx, 0); } Rebuild(); }, 56f);
        }

        private void DrawTimeAndFlags()
        {
            var save = Save();
            if (save == null) { Text(_content, "No save data loaded yet (start or load a game)."); return; }
            var cyc = save.Cycle;
            if (cyc != null)
            {
                var col = Column(_content, true);
                Text(col, $"<b>Time of day</b>: {cyc.StrTimeZone} (zone {cyc.TimeZone})", 15f);
                var r = Row(col);
                foreach (var (name, val) in new[] { ("Morning", 0), ("Afternoon", 1), ("Evening", 2), ("Night", 3), ("Night event", 4) })
                { int v = val; string n = name; Btn(r, n, () => { cyc.TimeZone = v; Say($"Time zone set to {n} (applies on the next map / scene change)."); Rebuild(); }, 110f); }
                Btn(Row(col), "Advance one time zone (game logic)", () =>
                {
                    try { cyc.IncrementTimeZone(null); Say("Advanced."); }
                    catch (Exception e) { cyc.TimeZone = (cyc.TimeZone + 1) % 5; Say($"Advanced (direct): {e.Message}"); }
                    Rebuild();
                }, 300f);
            }
            var c2 = Column(_content, true);
            Text(c2, $"<b>Save</b>: map {save.MapID}, tutorial progress {save.TutorialProgress}, entries {save.EntryCount}, item event {save.IsItemEvent}", 14f);
            var r2 = Row(c2);
            Btn(r2, "Tutorial: mark done", () => { save.TutorialProgress = 999; save.IsTutorialReplayed = true; Say("Tutorial flagged done."); Rebuild(); }, 170f);
            Btn(r2, "Tutorial: reset", () => { save.TutorialProgress = 0; Say("Tutorial reset."); Rebuild(); }, 140f);
            try { var pd = save.PlayerData; if (pd != null) Text(c2, $"Player: {NameOf(pd)}  personality {pd.Personality}  futanari {pd.IsFutanari}", 14f); } catch (Exception e) { Text(c2, e.Message, 13f); }
            var c3 = Column(_content, true);
            Text(c3, "<b>Global data</b> (help screens seen / unlock flags)", 15f);
            try
            {
                var gd = Manager.Game.Instance != null ? Manager.Game.Instance.GlobalData : null;
                if (gd != null)
                {
                    Text(c3, $"help states: {gd.HelpStates?.Count ?? 0}, flags: {gd.Flags?.Count ?? 0}, tutorial seen: {gd.TutorialSeen}", 14f);
                    var r3 = Row(c3);
                    Btn(r3, "Mark all help screens seen (0-199)", () => { for (int i = 0; i < 200; i++) gd.AddHelp(i); gd.TutorialSeen = true; gd.Save(); Say("Help screens marked seen."); Rebuild(); }, 290f);
                    Btn(r3, "Add flags 0-199", () => { for (int i = 0; i < 200; i++) gd.AddShortMovie(i); gd.Save(); Say("Flags added."); Rebuild(); }, 150f);
                }
            }
            catch (Exception e) { Text(c3, e.Message, 13f); }
        }

        private void DrawAbout()
        {
            var col = Column(_content, true);
            Text(col, "Edits the live save data in memory; save the game afterwards to keep changes.", 14f);
            Text(col, "Level buttons set LV and points together from the game's own PropertyData tables.", 14f);
            Text(col, "Time-of-day changes apply when the game next evaluates the cycle (map or scene change).", 14f);
            Text(col, "Level tables: " + string.Join(", ", Array.ConvertAll(new[] { 0, 1, 2, 3 }, i => { Table(i, out var m, out var pp); return $"{m} lv x {pp} pt"; })), 14f);
            Text(col, $"Font asset: {(Font() != null ? Font().name : "none found")}", 13f);
        }
    }
}
