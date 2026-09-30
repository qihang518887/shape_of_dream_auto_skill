using System;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AutoUseSkill
{
    public class AutoUseSkillConfig : ModConfig
    {
        [Header("快捷键设置 (Hotkeys)")]
        [LabelText("Q 技能自动释放快捷键")]
        public KeyCode keySkillQ = KeyCode.F1;

        [LabelText("W 技能自动释放快捷键")]
        public KeyCode keySkillW = KeyCode.F2;

        [LabelText("E 技能自动释放快捷键")]
        public KeyCode keySkillE = KeyCode.F3;

        [LabelText("R 技能自动释放快捷键")]
        public KeyCode keySkillR = KeyCode.F4;

        [LabelText("自动普通攻击快捷键")]
        public KeyCode keyAutoAttack = KeyCode.F5;

        [LabelText("自动击碎矿石/金币罐快捷键")]
        public KeyCode keyAutoProps = KeyCode.F6;

        [Space(10)]
        [Header("默认启动状态 (Default States)")]
        [LabelText("脱战时是否允许自动施法")]
        public bool autoCastOutOfCombat = false;

        [LabelText("游戏启动时默认开启自动普攻")]
        public bool defaultAutoAttack = false;

        [LabelText("游戏启动时默认开启自动敲矿/罐")]
        public bool defaultAutoProps = true;

        [Space(10)]
        [Header("蓄力技能设置 (Charge Skills)")]
        [LabelText("蓄力技能满蓄自动释放")]
        public bool autoReleaseFullCharge = true;

        [Space(10)]
        [Header("性能检测 (Performance)")]
        [LabelText("检测扫描间隔 (秒)")]
        public float searchInterval = 0.05f;
    }

    public class AutoUseSkill : ModBehaviour
    {
        private const int WindowId = 894270;

        // Native Mod Configuration integration
        public AutoUseSkillConfig config = new AutoUseSkillConfig();

        private bool ShowGUI;
        private KeyCode OpenMenuKey = KeyCode.I;
        private float ButtonHeight = 0.035f;
        private Rect WindowRect;

        private bool Auto_Attack;
        private bool Auto_Attack_Props = true;
        private bool[] AutoUseDict = new bool[4];
        private float lastSearchTime = 0f;

        private string feedbackText = "";
        private float feedbackEndTime = 0f;

        private bool isInCombatRoom = false;

        // Native UGUI TextMeshPro Overlay Components
        private Canvas overlayCanvas;
        private TextMeshProUGUI[] skillLabels = new TextMeshProUGUI[4];
        private TextMeshProUGUI attackLabel;
        private TextMeshProUGUI propsLabel;
        private bool hudStyled = false;
        private Vector3[] cornersBuffer = new Vector3[4];

        private static readonly Color ColorOn = new Color(0.2f, 1f, 0.4f, 1f);
        private static readonly Color ColorOff = new Color(1f, 0.3f, 0.3f, 1f);

        private bool IsInCombatRoom
        {
            get { return isInCombatRoom; }
        }

        private Hero Player
        {
            get
            {
                if (ManagerBase<ControlManager>.instance != null)
                {
                    return ManagerBase<ControlManager>.instance.controllingEntity as Hero;
                }
                return null;
            }
        }

        private ControlManager controlManager
        {
            get { return ManagerBase<ControlManager>.instance; }
        }

        private void Awake()
        {
            Debug.Log("[AutoUseSkill] Mod loaded with native UGUI HUD and ModConfig!");
            WindowRect = new Rect(Screen.width * 0.35f, Screen.height * 0.4f, Screen.width * 0.30f, 0f);

            // Initialize runtime state from saved configuration
            if (config != null)
            {
                Auto_Attack = config.defaultAutoAttack;
                Auto_Attack_Props = config.defaultAutoProps;
            }

            UpdateCombatRoomStatus(SceneManager.GetActiveScene());
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        public override void OnConfigChanged()
        {
            base.OnConfigChanged();
            Debug.Log("[AutoUseSkill] Configuration updated via Settings menu.");
        }

        private void OnDestroy()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            ShowGUI = false;
            Auto_Attack = false;
            Auto_Attack_Props = true;
            if (AutoUseDict != null)
            {
                for (int i = 0; i < AutoUseDict.Length; i++)
                {
                    AutoUseDict[i] = false;
                }
            }

            if (overlayCanvas != null && overlayCanvas.gameObject != null)
            {
                Destroy(overlayCanvas.gameObject);
                overlayCanvas = null;
            }

            Debug.Log("[AutoUseSkill] Mod unloaded.");
        }

        private void OnActiveSceneChanged(Scene current, Scene next)
        {
            UpdateCombatRoomStatus(next);
            if (!isInCombatRoom && overlayCanvas != null && overlayCanvas.gameObject.activeSelf)
            {
                overlayCanvas.gameObject.SetActive(false);
            }
        }

        private void UpdateCombatRoomStatus(Scene scene)
        {
            try
            {
                string sceneName = scene.name;
                isInCombatRoom = sceneName != null && sceneName.StartsWith("Room_");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[AutoUseSkill] UpdateCombatRoomStatus error: " + ex.Message);
                isInCombatRoom = false;
            }
        }

        private void ShowFeedback(string text)
        {
            feedbackText = text;
            feedbackEndTime = Time.time + 1.5f;
        }

        private static bool IsAttackableProp(Entity e)
        {
            if (e == null || !e.isActive || !e.isAlive || e.isDead) return false;
            if (e.Status != null && e.Status.isDead) return false;

            // Known breakable resource objects first (Dream Dust, Gold Pots, Nightmare Stones)
            if (e is PropEnt_Stone_DreamDust || e is PropEnt_Stone_Gold || e is PropEnt_Stone_Nightmare)
                return true;

            // Never attack merchants, shopkeepers, or interactable NPCs
            if (e is PropEnt_Merchant_Base || e is PropEnt_Merchant_Backpack || e is IInteractable)
                return false;

            // Generic destructible props (excluding players, monsters, summons)
            if (e is PropEntity && !(e is Monster) && !(e is Hero) && !(e is Summon))
            {
                return true;
            }

            return false;
        }

        private void Update()
        {
            if (Input.GetKeyDown(OpenMenuKey))
                ShowGUI = !ShowGUI;

            // Zero overhead when outside actual combat rooms (in Lobby, Traveler settings, Title, etc.)
            if (!IsInCombatRoom)
            {
                if (overlayCanvas != null && overlayCanvas.gameObject.activeSelf)
                {
                    overlayCanvas.gameObject.SetActive(false);
                }
                return;
            }

            // Hotkey detection (dynamically bound to native ModConfig)
            KeyCode kQ = config != null ? config.keySkillQ : KeyCode.F1;
            KeyCode kW = config != null ? config.keySkillW : KeyCode.F2;
            KeyCode kE = config != null ? config.keySkillE : KeyCode.F3;
            KeyCode kR = config != null ? config.keySkillR : KeyCode.F4;
            KeyCode kAtk = config != null ? config.keyAutoAttack : KeyCode.F5;
            KeyCode kProps = config != null ? config.keyAutoProps : KeyCode.F6;

            if (Input.GetKeyDown(kQ))
            {
                AutoUseDict[0] = !AutoUseDict[0];
                ShowFeedback(string.Format("Q 技能自动释放: {0}", AutoUseDict[0] ? "开启 (ON)" : "关闭 (OFF)"));
            }
            if (Input.GetKeyDown(kW))
            {
                AutoUseDict[1] = !AutoUseDict[1];
                ShowFeedback(string.Format("W 技能自动释放: {0}", AutoUseDict[1] ? "开启 (ON)" : "关闭 (OFF)"));
            }
            if (Input.GetKeyDown(kE))
            {
                AutoUseDict[2] = !AutoUseDict[2];
                ShowFeedback(string.Format("E 技能自动释放: {0}", AutoUseDict[2] ? "开启 (ON)" : "关闭 (OFF)"));
            }
            if (Input.GetKeyDown(kR))
            {
                AutoUseDict[3] = !AutoUseDict[3];
                ShowFeedback(string.Format("R 技能自动释放: {0}", AutoUseDict[3] ? "开启 (ON)" : "关闭 (OFF)"));
            }
            if (Input.GetKeyDown(kAtk))
            {
                Auto_Attack = !Auto_Attack;
                ShowFeedback(string.Format("自动普通攻击: {0}", Auto_Attack ? "开启 (ON)" : "关闭 (OFF)"));
            }
            if (Input.GetKeyDown(kProps))
            {
                Auto_Attack_Props = !Auto_Attack_Props;
                ShowFeedback(string.Format("自动击碎矿石/金币罐: {0}", Auto_Attack_Props ? "开启 (ON)" : "关闭 (OFF)"));
            }

            // Update Native UGUI HUD badges
            UpdateHud();

            // Local cache to guard against TOCTOU null reference
            Hero player = Player;
            if (player == null) return;

            bool hasAnySkillOn = AutoUseDict[0] || AutoUseDict[1] || AutoUseDict[2] || AutoUseDict[3];
            bool allowOutOfCombat = config != null && config.autoCastOutOfCombat;
            bool canCastSkill = hasAnySkillOn && (allowOutOfCombat || player.isInCombat);
            bool canAttack = Auto_Attack || Auto_Attack_Props;

            // Early return if all auto features are off: zero entity search overhead!
            if (!canCastSkill && !canAttack) return;

            float interval = (config != null && config.searchInterval > 0.01f) ? config.searchInterval : 0.05f;
            if (Time.time - lastSearchTime < interval) return;
            lastSearchTime = Time.time;

            ControlManager cm = controlManager;

            // Determine hero attack range for prop scanning
            float attackRange = 4.5f;
            if (player.Ability != null && player.Ability.attackAbility != null && player.Ability.attackAbility.currentConfig != null)
            {
                attackRange = player.Ability.attackAbility.currentConfig.effectiveRange;
            }
            if (attackRange < 2.5f) attackRange = 2.5f;

            // Single pass search for closest enemy (up to 25m for skills) and closest prop (within attack range + 2m)
            Entity closestEnemy;
            Entity closestProp;
            FindClosestTargets(player, 25f, attackRange + 2f, out closestEnemy, out closestProp);

            // Check and handle ongoing charge skill (natural charging -> full charge auto-release)
            if (TryHandleActiveCharge(player, cm, closestEnemy))
            {
                return;
            }

            if (canCastSkill)
            {
                TryAutoCastSkill(player, cm, closestEnemy);
            }
            if (canAttack)
            {
                TryAutoAttack(player, closestEnemy, closestProp, attackRange);
            }
        }

        private void FindClosestTargets(Hero player, float maxEnemyRange, float maxPropRange, out Entity closestEnemy, out Entity closestProp)
        {
            closestEnemy = null;
            closestProp = null;
            if (player == null) return;

            float minEnemyDistSq = maxEnemyRange * maxEnemyRange;
            float minPropDistSq = maxPropRange * maxPropRange;
            Vector3 playerPos = player.agentPosition;

            try
            {
                if (NetworkedManagerBase<ActorManager>.instance != null && NetworkedManagerBase<ActorManager>.instance.allEntities != null)
                {
                    foreach (Entity e in NetworkedManagerBase<ActorManager>.instance.allEntities)
                    {
                        if (e == null || !e.isActive || !e.isAlive || e.isDead) continue;
                        if (e.Status != null && (e.Status.isDead || e.Status.isUndetectableByNonAllies)) continue;

                        if (player.GetRelation(e) == EntityRelation.Enemy)
                        {
                            float dSq = (e.agentPosition - playerPos).sqrMagnitude;
                            if (dSq < minEnemyDistSq)
                            {
                                minEnemyDistSq = dSq;
                                closestEnemy = e;
                            }
                        }
                        else if (Auto_Attack_Props && IsAttackableProp(e))
                        {
                            float dSq = (e.agentPosition - playerPos).sqrMagnitude;
                            if (dSq < minPropDistSq)
                            {
                                minPropDistSq = dSq;
                                closestProp = e;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[AutoUseSkill] FindClosestTargets error: " + ex.Message);
            }
        }

        private Entity FindClosestValidEnemy(Hero player, TriggerConfig config, float maxRange = 25f)
        {
            if (player == null || config == null) return null;
            Entity bestEnemy = null;
            float minDistSq = maxRange * maxRange;
            Vector3 playerPos = player.agentPosition;

            try
            {
                var actorMgr = NetworkedManagerBase<ActorManager>.instance;
                if (actorMgr != null && actorMgr.allEntities != null)
                {
                    foreach (Entity e in actorMgr.allEntities)
                    {
                        if (e == null || !e.isActive || !e.isAlive || e.isDead) continue;
                        if (e.Status != null && (e.Status.isDead || e.Status.isUndetectableByNonAllies)) continue;

                        if (player.GetRelation(e) != EntityRelation.Enemy) continue;

                        if (config.targetValidator != null && !config.targetValidator.Evaluate(player, e)) continue;
                        if (!config.CheckRange(player, e)) continue;

                        float dSq = (e.agentPosition - playerPos).sqrMagnitude;
                        if (dSq < minDistSq)
                        {
                            minDistSq = dSq;
                            bestEnemy = e;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[AutoUseSkill] FindClosestValidEnemy error: " + ex.Message);
            }

            return bestEnemy;
        }

        private static Action<DewPlayer, CastInfo> dispatchSampleCastDelegate;

        private static void DispatchNativeSampleCast(CastInfo info)
        {
            try
            {
                if (DewPlayer.local == null) return;

                if (dispatchSampleCastDelegate == null)
                {
                    MethodInfo mi = typeof(DewPlayer).GetMethod(
                        "DispatchSample_Cast",
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
                    );
                    if (mi != null)
                    {
                        dispatchSampleCastDelegate = (Action<DewPlayer, CastInfo>)Delegate.CreateDelegate(
                            typeof(Action<DewPlayer, CastInfo>),
                            mi
                        );
                    }
                }

                if (dispatchSampleCastDelegate != null)
                {
                    dispatchSampleCastDelegate(DewPlayer.local, info);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[AutoUseSkill] DispatchNativeSampleCast error: " + ex.Message);
            }
        }

        private bool TryHandleActiveCharge(Hero player, ControlManager cm, Entity closestEnemy)
        {
            if (config == null || !config.autoReleaseFullCharge || cm == null || !cm.localSampleContext.HasValue)
            {
                return false;
            }

            SampleCastInfoContext ctx = cm.localSampleContext.Value;
            AbilityTrigger trigger = ctx.trigger;
            if (trigger == null || trigger.IsNullOrInactive()) return false;

            SkillTrigger skill = trigger as SkillTrigger;
            if (skill == null) return false;

            // Check if this skill slot is enabled in AutoUseDict
            int slot = -1;
            if (player != null && player.Ability != null && player.Ability.abilities != null)
            {
                for (int i = 0; i < 4; i++)
                {
                    AbilityTrigger ab;
                    if (player.Ability.abilities.TryGetValue(i, out ab) && ab == trigger)
                    {
                        slot = i;
                        break;
                    }
                }
            }

            // If the player is manually charging a skill whose auto toggle is OFF, do not interfere!
            if (slot >= 0 && slot < 4 && !AutoUseDict[slot])
            {
                return false;
            }

            // Check charge progress (in Shape of Dreams, fillAmount reaches 1.0f on full charge)
            if (trigger.fillAmount < 0.98f)
            {
                // Still charging: return true to pause other actions and let charge accumulate
                return true;
            }

            // Reached full charge! Re-target nearest enemy and fire
            TriggerConfig cfg = trigger.currentConfig;
            Entity target = null;

            if (closestEnemy != null && (cfg == null || cfg.targetValidator == null || cfg.targetValidator.Evaluate(player, closestEnemy)) && (cfg == null || cfg.CheckRange(player, closestEnemy)))
            {
                target = closestEnemy;
            }
            else if (cfg != null)
            {
                target = FindClosestValidEnemy(player, cfg, 25f);
            }

            CastInfo releaseInfo;
            if (target != null)
            {
                releaseInfo = trigger.GetPredictedCastInfoToTarget(target);
            }
            else
            {
                releaseInfo = ctx.currentInfo;
            }

            DispatchNativeSampleCast(releaseInfo);
            return true;
        }

        private void TryAutoCastSkill(Hero player, ControlManager cm, Entity closestEnemy)
        {
            if (player == null || cm == null) return;

            // If hero is actively channeling an ongoing attack (e.g. Yubar continuous laser beam)
            // or an ongoing channeled ability, do not interrupt it with auto-cast skills!
            if (player.Ability != null && player.Ability.attackAbility != null && player.Ability.attackAbility.Network_isCasting)
            {
                return;
            }
            if (player.Control != null && player.Control.ongoingChannels != null && player.Control.ongoingChannels.Count > 0)
            {
                return;
            }

            for (var i = 0; i <= 3; i++)
            {
                if (!AutoUseDict[i]) continue;
                AbilityTrigger abilityTrigger;
                if (!player.Ability.abilities.TryGetValue(i, out abilityTrigger)) continue;
                SkillTrigger skill = abilityTrigger as SkillTrigger;
                if (skill == null || skill.IsNullOrInactive()) continue;

                // Protect continuous channeling skills from being disrupted
                if (skill.Network_isCasting) continue;
                if (!skill.CanBeCast()) continue;

                TriggerConfig config = skill.currentConfig;
                if (config == null) continue;

                EntityRelation targets = config.targetValidator != null ? config.targetValidator.targets : (EntityRelation)0;
                bool canTargetSelf = (targets & EntityRelation.Self) != 0;
                bool canTargetEnemy = (targets & EntityRelation.Enemy) != 0;
                CastMethodType castType = config.castMethod != null ? config.castMethod.type : CastMethodType.None;

                // Identify if this is a defensive / shield / heal / self-buff skill:
                // 1. Can target Self but CANNOT target Enemy, OR
                // 2. Cannot target Enemy and is non-targeted (None) or victim is Caster
                bool isSelfOrDefensive = false;
                if (canTargetSelf && !canTargetEnemy)
                {
                    isSelfOrDefensive = true;
                }
                else if (!canTargetEnemy && (castType == CastMethodType.None || config.victim == TriggerConfig.StatusEffectVictimType.Caster))
                {
                    isSelfOrDefensive = true;
                }

                try
                {
                    // === CASE 1: Shield / Heal / Self-Buff Skills ===
                    // Must ALWAYS target the player, never a distant enemy!
                    if (isSelfOrDefensive)
                    {
                        if (castType == CastMethodType.Cone || castType == CastMethodType.Arrow)
                        {
                            cm.CastAbilityAuto(skill);
                        }
                        else
                        {
                            cm.CastAbility(skill, skill.GetCastInfoToTarget(player), false);
                        }
                        continue;
                    }

                    // === CASE 2: Offensive Skills (targeting Enemy) ===
                    Entity enemyTarget = null;

                    // 1. Prefer closest living enemy if valid for this skill and within range
                    if (closestEnemy != null && (config.targetValidator == null || config.targetValidator.Evaluate(player, closestEnemy)) && config.CheckRange(player, closestEnemy))
                    {
                        enemyTarget = closestEnemy;
                    }
                    else
                    {
                        // 2. Otherwise search for the closest living enemy that satisfies this skill's validator and range
                        enemyTarget = FindClosestValidEnemy(player, config, 25f);
                    }

                    // If an enemy is in effective range: cast offensive skill at enemy using native predicted targeting
                    if (enemyTarget != null)
                    {
                        cm.CastAbility(skill, skill.GetPredictedCastInfoToTarget(enemyTarget), false);
                    }
                    else if (canTargetSelf)
                    {
                        // Fallback for hybrid skills that can target self when no enemies are nearby
                        cm.CastAbility(skill, skill.GetCastInfoToTarget(player), false);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[AutoUseSkill] CastAbility error: " + ex.Message);
                }
            }
        }

        private void TryAutoAttack(Hero player, Entity closestEnemy, Entity closestProp, float attackRange)
        {
            if ((!Auto_Attack && !Auto_Attack_Props) || player == null) return;
            AbilityTrigger attackAbility = player.Ability.attackAbility;
            if (attackAbility == null || attackAbility.IsNullOrInactive()) return;

            // Continuous channel check (e.g. Yubar laser beam): do not disrupt active channel!
            if (attackAbility.Network_isCasting) return;
            if (player.Control != null && player.Control.ongoingChannels != null && player.Control.ongoingChannels.Count > 0) return;

            if (!attackAbility.CanBeCast()) return;

            Entity target = null;

            // 1. If Auto_Attack is enabled: search for aggressive enemies within attack range first
            if (Auto_Attack)
            {
                // 1a. Native attack-move target finder (prioritizes aggressive enemies)
                try
                {
                    target = ActionAttackMove.FindAttackMoveTarget(player, player.agentPosition);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[AutoUseSkill] FindAttackMoveTarget error: " + ex.Message);
                }

                // If FindAttackMoveTarget returned an invalid target or a target outside attack range, discard it
                if (target != null)
                {
                    bool isValidEnemy = target.isActive && target.isAlive && player.GetRelation(target) == EntityRelation.Enemy;
                    bool inRange = attackAbility.currentConfig != null && attackAbility.currentConfig.CheckRange(player, target);
                    if (!isValidEnemy || !inRange)
                    {
                        target = null;
                    }
                }

                // 1b. Fallback to closest enemy within attack range
                if (target == null && closestEnemy != null && closestEnemy.isActive && closestEnemy.isAlive)
                {
                    float dSq = (closestEnemy.agentPosition - player.agentPosition).sqrMagnitude;
                    if (dSq <= attackRange * attackRange)
                    {
                        target = closestEnemy;
                    }
                }
            }

            // 2. Priority 2: If no enemies are within attack range, auto-target nearest destructible prop (Dream Dust, Gold Pot, etc.)
            if (target == null && Auto_Attack_Props && closestProp != null && closestProp.isActive && closestProp.isAlive)
            {
                float dSq = (closestProp.agentPosition - player.agentPosition).sqrMagnitude;
                if (dSq <= attackRange * attackRange)
                {
                    target = closestProp;
                }
            }

            if (target == null || !target.isActive || !target.isAlive) return;

            // Final range validation
            if (attackAbility.currentConfig != null && !attackAbility.currentConfig.CheckRange(player, target))
            {
                return;
            }

            // 3. Target lock check:
            // If the player is already locked onto and actively attacking this exact target,
            // DO NOT re-issue CmdAttack. Re-issuing CmdAttack every tick resets the attack animation
            // and cuts off continuous channeled beams (such as Yubar's beam).
            if (player.Control != null && player.Control.attackTarget == target)
            {
                return;
            }

            // 4. Issue native server attack command without canceling movement (cancelMovement = false)
            try
            {
                player.Control.CmdAttack(target, false);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[AutoUseSkill] CmdAttack error: " + ex.Message);
            }
        }

        // ==========================================
        // Native UGUI TextMeshPro Indicator System
        // ==========================================
        private void EnsureHudOverlay()
        {
            if (overlayCanvas != null) return;

            GameObject go = new GameObject("AutoUseSkill_OverlayCanvas");
            DontDestroyOnLoad(go);
            overlayCanvas = go.AddComponent<Canvas>();
            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlayCanvas.sortingOrder = 32760;

            for (int i = 0; i < 4; i++)
            {
                skillLabels[i] = CreateHudLabel(go.transform, "SkillBadge_" + i, new Vector2(70f, 24f));
            }

            attackLabel = CreateHudLabel(go.transform, "AtkBadge", new Vector2(100f, 26f));
            propsLabel = CreateHudLabel(go.transform, "PropsBadge", new Vector2(100f, 26f));
        }

        private TextMeshProUGUI CreateHudLabel(Transform parent, string name, Vector2 size)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            RectTransform rt = tmp.rectTransform;
            rt.sizeDelta = size;
            obj.SetActive(false);
            return tmp;
        }

        private void EnsureHudStyled(UI_InGame_SkillButton[] btns)
        {
            if (hudStyled) return;

            TMP_FontAsset font = null;
            if (btns != null)
            {
                for (int i = 0; i < btns.Length; i++)
                {
                    if (btns[i] != null)
                    {
                        TextMeshProUGUI existing = btns[i].GetComponentInChildren<TextMeshProUGUI>(true);
                        if (existing != null && existing.font != null)
                        {
                            font = existing.font;
                            break;
                        }
                    }
                }
            }

            if (font == null) return;

            StyleHudLabel(attackLabel, font, 13f);
            StyleHudLabel(propsLabel, font, 13f);
            for (int i = 0; i < 4; i++)
            {
                StyleHudLabel(skillLabels[i], font, 12f);
            }
            hudStyled = true;
        }

        private void StyleHudLabel(TextMeshProUGUI label, TMP_FontAsset font, float size)
        {
            if (label == null) return;
            label.font = font;
            label.fontSize = size;
            label.fontStyle = FontStyles.Bold;
            if (label.fontMaterial != null)
            {
                label.fontMaterial.SetColor(ShaderUtilities.ID_OutlineColor, Color.black);
                label.fontMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.25f);
                label.fontMaterial.SetFloat(ShaderUtilities.ID_FaceDilate, 0.15f);
            }
            label.UpdateMeshPadding();
        }

        private void UpdateHud()
        {
            if (!IsInCombatRoom || ManagerBase<UI_InGame_SkillButtons>.instance == null || ManagerBase<UI_InGame_SkillButtons>.instance.skillButtons == null)
            {
                if (overlayCanvas != null && overlayCanvas.gameObject.activeSelf)
                {
                    overlayCanvas.gameObject.SetActive(false);
                }
                return;
            }

            EnsureHudOverlay();
            if (!overlayCanvas.gameObject.activeSelf)
            {
                overlayCanvas.gameObject.SetActive(true);
            }

            UI_InGame_SkillButton[] btns = ManagerBase<UI_InGame_SkillButtons>.instance.skillButtons;
            EnsureHudStyled(btns);

            Vector3 qPos = Vector3.zero;
            bool foundQ = false;

            for (int i = 0; i < btns.Length; i++)
            {
                UI_InGame_SkillButton btn = btns[i];
                if (btn == null || !btn.isActiveAndEnabled) continue;

                int slot = -1;
                switch (btn.skillType)
                {
                    case HeroSkillLocation.Q: slot = 0; break;
                    case HeroSkillLocation.W: slot = 1; break;
                    case HeroSkillLocation.E: slot = 2; break;
                    case HeroSkillLocation.R: slot = 3; break;
                }

                if (slot >= 0 && slot < 4)
                {
                    RectTransform iconRt = btn.icon != null ? btn.icon.rectTransform : (btn.transform as RectTransform);
                    if (iconRt != null)
                    {
                        iconRt.GetWorldCorners(cornersBuffer);
                        Canvas parentCanvas = iconRt.GetComponentInParent<Canvas>();
                        Camera cam = (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay) ? parentCanvas.worldCamera : null;

                        Vector2 screenBL = RectTransformUtility.WorldToScreenPoint(cam, cornersBuffer[0]);
                        Vector2 screenTR = RectTransformUtility.WorldToScreenPoint(cam, cornersBuffer[2]);

                        float cx = Mathf.Lerp(screenBL.x, screenTR.x, 0.5f);
                        float cy = Mathf.Lerp(screenBL.y, screenTR.y, 0.72f);
                        Vector3 badgePos = new Vector3(cx, cy, 0f);

                        if (slot == 0)
                        {
                            qPos = badgePos;
                            foundQ = true;
                        }

                        TextMeshProUGUI label = skillLabels[slot];
                        if (label != null)
                        {
                            if (!label.gameObject.activeSelf) label.gameObject.SetActive(true);
                            label.transform.position = badgePos;
                            bool isOn = AutoUseDict[slot];
                            label.text = isOn ? "ON" : "OFF";
                            label.color = isOn ? ColorOn : ColorOff;
                        }
                    }
                }
            }

            // Auto Attack and Prop Badges placed neatly to the left of Q
            if (foundQ)
            {
                if (attackLabel != null)
                {
                    if (!attackLabel.gameObject.activeSelf) attackLabel.gameObject.SetActive(true);
                    attackLabel.transform.position = new Vector3(qPos.x - 70f, qPos.y, 0f);
                    attackLabel.text = Auto_Attack ? "普攻 ON" : "普攻 OFF";
                    attackLabel.color = Auto_Attack ? ColorOn : ColorOff;
                }

                if (propsLabel != null)
                {
                    if (!propsLabel.gameObject.activeSelf) propsLabel.gameObject.SetActive(true);
                    propsLabel.transform.position = new Vector3(qPos.x - 165f, qPos.y, 0f);
                    propsLabel.text = Auto_Attack_Props ? "敲矿 ON" : "敲矿 OFF";
                    propsLabel.color = Auto_Attack_Props ? ColorOn : ColorOff;
                }
            }
        }

        private void OnGUI()
        {
            if (!IsInCombatRoom)
            {
                if (ShowGUI)
                {
                    WindowRect = GUILayout.Window(WindowId, WindowRect, MenuGui, "Auto Use Skill", "box");
                }
                return;
            }

            // 1. Toast floating feedback banner (1.5 seconds)
            if (Time.time < feedbackEndTime && !string.IsNullOrEmpty(feedbackText))
            {
                GUI.Box(new Rect(Screen.width * 0.38f, 15f, Screen.width * 0.24f, 32f), feedbackText);
            }

            // 2. Settings window (optional quick menu)
            if (!ShowGUI) return;
            WindowRect = GUILayout.Window(WindowId, WindowRect, MenuGui, "Auto Use Skill", "box");
        }

        private void MenuGui(int id)
        {
            GUI.DragWindow(new Rect(0, 0, 1000, Screen.height * ButtonHeight));
            var option = GUILayout.Height(Screen.height * ButtonHeight);
            GUILayout.BeginVertical();
            GUILayout.Label("", option);
            GUILayout.BeginHorizontal();
            if (config != null)
            {
                config.autoCastOutOfCombat = GUILayout.Toggle(config.autoCastOutOfCombat, "脱战施法", option);
            }
            Auto_Attack = GUILayout.Toggle(Auto_Attack, "自动普攻", option);
            Auto_Attack_Props = GUILayout.Toggle(Auto_Attack_Props, "自动敲矿/罐", option);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            AutoUseDict[0] = GUILayout.Toggle(AutoUseDict[0], "Q", option);
            AutoUseDict[1] = GUILayout.Toggle(AutoUseDict[1], "W", option);
            AutoUseDict[2] = GUILayout.Toggle(AutoUseDict[2], "E", option);
            AutoUseDict[3] = GUILayout.Toggle(AutoUseDict[3], "R", option);
            GUILayout.EndHorizontal();
            float interval = config != null ? config.searchInterval : 0.05f;
            GUILayout.Label("检测间隔 (Interval): " + interval.ToString("0.00") + "s", option);
            if (config != null)
            {
                config.searchInterval = (float)Math.Round(GUILayout.HorizontalSlider(config.searchInterval, 0.03f, 0.5f, option), 2);
            }
            GUILayout.EndVertical();
        }

        [ConsoleCommand("autoskill", "切换自动施法GUI显示")]
        public void CmdToggleGui()
        {
            ShowGUI = !ShowGUI;
        }
    }
}
