using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BelleEpoque.Core;
using UnityEngine;

namespace BelleEpoque
{
    /// <summary>
    /// Ponte entre a lógica pura (BattleSystem) e a cena:
    /// cria as unidades a partir dos ScriptableObjects, instancia os modelos,
    /// e reproduz cada BattleEvent com animação, som, VFX e UI.
    /// </summary>
    public class BattleController : MonoBehaviour
    {
        [Header("Dados")]
        [SerializeField] private EncounterDefinition encounter;
        [Tooltip("0 = aleatório. Use um número fixo para repetir a mesma batalha ao testar.")]
        [SerializeField] private int randomSeed = 0;

        [Header("Cena")]
        [SerializeField] private Transform[] heroSpawnPoints;
        [SerializeField] private Transform[] enemySpawnPoints;
        [Tooltip("Usado quando a unidade não tem Model Prefab.")]
        [SerializeField] private GameObject fallbackModel;
        [SerializeField] private GameObject turnIndicator;

        [Header("Sistemas")]
        [SerializeField] private BattleHUD hud;
        [SerializeField] private AudioManager audioManager;
        [SerializeField] private SanityPostFx sanityFx;
        [SerializeField] private CameraShake cameraShake;

        [Header("Ritmo")]
        [SerializeField] private float pauseBetweenEvents = 0.35f;

        [Header("Cores dos números")]
        [SerializeField] private Color damageColor = new Color(1f, 0.85f, 0.75f);
        [SerializeField] private Color criticalColor = new Color(1f, 0.35f, 0.25f);
        [SerializeField] private Color healColor = new Color(0.55f, 1f, 0.6f);
        [SerializeField] private Color sanityColor = new Color(0.75f, 0.55f, 1f);
        [SerializeField] private Color infoColor = new Color(0.95f, 0.85f, 0.5f);

        private BattleSystem _battle;
        private readonly Dictionary<BattleUnit, UnitView> _views = new Dictionary<BattleUnit, UnitView>();
        private readonly Dictionary<SkillDefinition, SkillData> _skillCache = new Dictionary<SkillDefinition, SkillData>();
        private UnitView _awayView;
        private bool _busy;

        public BattleSystem Battle => _battle;

        private void Start()
        {
            if (encounter == null)
            {
                Debug.LogError("[BattleController] Nenhum EncounterDefinition atribuído.", this);
                enabled = false;
                return;
            }

            // Campos não atribuídos no Inspector viram "null falso" da Unity; convertemos para null real
            // para que os operadores ?. funcionem com segurança.
            if (!hud) hud = null;
            if (!audioManager) audioManager = null;
            if (!sanityFx) sanityFx = null;
            if (!cameraShake) cameraShake = null;
            if (!turnIndicator) turnIndicator = null;

            SetupBattle();
            SetupCamera();
            if (audioManager != null)
            {
                audioManager.PlayMusic(encounter.battleMusic);
                audioManager.PlayAmbience(encounter.ambienceLoop);
            }
            StartCoroutine(Run(_battle.Start()));
        }

        // ------------------------------------------------------------------ Montagem

        private SkillData GetSkill(SkillDefinition def)
        {
            if (def == null) return null;
            if (!_skillCache.TryGetValue(def, out var data))
            {
                data = def.CreateData();
                _skillCache[def] = data;
            }
            return data;
        }

        private BattleUnit CreateUnit(UnitDefinition def, Team team, int index)
        {
            var basic = GetSkill(def.basicAttack) ?? new SkillData { Id = "basic", Name = "Ataque" };
            var skills = def.skills.Where(s => s != null).Select(GetSkill);
            return new BattleUnit($"{def.name}_{index}", def.displayName, team, def.BuildStats(), basic, skills,
                def.weaknesses, def.resistances, def.trilha, def.IsAgent ? def.nex : 0)
            {
                Tag = def
            };
        }

        private void SetupBattle()
        {
            var heroes = new List<BattleUnit>();
            var enemies = new List<BattleUnit>();

            for (int i = 0; i < encounter.heroes.Count; i++)
            {
                var unit = CreateUnit(encounter.heroes[i], Team.Heroes, i);
                heroes.Add(unit);
                SpawnView(unit, encounter.heroes[i], SpawnPoint(heroSpawnPoints, i));
            }
            for (int i = 0; i < encounter.enemies.Count; i++)
            {
                var unit = CreateUnit(encounter.enemies[i], Team.Enemies, i);
                enemies.Add(unit);
                SpawnView(unit, encounter.enemies[i], SpawnPoint(enemySpawnPoints, i));
            }

            var inventory = new Dictionary<SkillData, int>();
            foreach (var stack in encounter.items)
                if (stack != null && stack.item != null) inventory[GetSkill(stack.item)] = stack.quantity;

            IRandom rng = randomSeed != 0 ? new SystemRandom(randomSeed) : new SystemRandom();
            _battle = new BattleSystem(heroes, enemies, rng, new SimpleEnemyAI(), inventory);

            if (hud != null) hud.Build(this, _battle);
            if (turnIndicator != null)
            {
                turnIndicator.SetActive(false);
                // Segundo anel, carmim, para mostrar o alvo que está sendo escolhido
                _targetIndicator = Instantiate(turnIndicator, turnIndicator.transform.parent);
                _targetIndicator.name = "Indicador de Alvo";
                foreach (var r in _targetIndicator.GetComponentsInChildren<Renderer>())
                {
                    var mpb = new MaterialPropertyBlock();
                    mpb.SetColor("_BaseColor", new Color(0.35f, 0.02f, 0.05f));
                    mpb.SetColor("_EmissionColor", new Color(2.4f, 0.15f, 0.2f));
                    r.SetPropertyBlock(mpb);
                }
                _targetIndicator.SetActive(false);
            }
        }

        private GameObject _targetIndicator;
        private BattleCamera _battleCamera;

        /// <summary>Coloca a Main Camera dentro de um rig que a BattleCamera move.</summary>
        private void SetupCamera()
        {
            var cam = Camera.main;
            if (cam == null) return;
            _battleCamera = cam.GetComponentInParent<BattleCamera>();
            if (_battleCamera == null)
            {
                var rig = new GameObject("CameraRig");
                rig.transform.SetPositionAndRotation(cam.transform.position, cam.transform.rotation);
                cam.transform.SetParent(rig.transform, true);
                cam.transform.localPosition = Vector3.zero;
                cam.transform.localRotation = Quaternion.identity;
                _battleCamera = rig.AddComponent<BattleCamera>();
            }
            if (cameraShake != null) cameraShake.Rebase();
            _battleCamera.Init(EnemyCenter);
        }

        /// <summary>
        /// Chamado pela HUD. Escolhendo alvo entre as ameaças: câmera sobe para mostrá-las.
        /// Escolhendo aliado: plano aberto. Fora disso: volta para o ombro do agente.
        /// </summary>
        public void SetTargeting(BattleUnit actor, bool targeting, bool enemies)
        {
            if (_battleCamera == null) return;
            var view = ViewOf(actor);
            if (view == null || actor.Team != Team.Heroes) { _battleCamera.Wide(); return; }
            if (!targeting) _battleCamera.Focus(view.transform.position);
            else if (enemies) _battleCamera.FocusTargets(view.transform.position);
            else _battleCamera.Wide();
        }

        private Vector3 EnemyCenter()
        {
            Vector3 sum = Vector3.zero;
            int n = 0;
            foreach (var u in _battle.Enemies)
            {
                var v = ViewOf(u);
                if (v == null || !u.IsAlive) continue;
                sum += v.transform.position;
                n++;
            }
            return n > 0 ? sum / n : Vector3.forward * 5f;
        }

        /// <summary>Mostra (ou esconde, com null) o anel sob o alvo em foco no menu.</summary>
        public void PreviewTarget(BattleUnit unit)
        {
            if (_targetIndicator == null) return;
            var view = ViewOf(unit);
            if (view == null) { _targetIndicator.SetActive(false); return; }
            var p = view.transform.position;
            _targetIndicator.transform.position = new Vector3(p.x, _targetIndicator.transform.position.y + 0.001f, p.z);
            _targetIndicator.SetActive(true);
        }

        private static Transform SpawnPoint(Transform[] points, int index)
        {
            if (points == null || points.Length == 0) return null;
            return points[Mathf.Min(index, points.Length - 1)];
        }

        private void SpawnView(BattleUnit unit, UnitDefinition def, Transform spawn)
        {
            var prefab = def.modelPrefab != null ? def.modelPrefab : fallbackModel;
            Vector3 pos = spawn != null ? spawn.position : Vector3.zero;
            Quaternion rot = spawn != null ? spawn.rotation : Quaternion.identity;

            GameObject go = prefab != null ? Instantiate(prefab, pos, rot) : GameObject.CreatePrimitive(PrimitiveType.Capsule);
            if (prefab == null) { go.transform.SetPositionAndRotation(pos + Vector3.up, rot); }
            go.name = def.displayName;
            go.transform.localScale *= def.modelScale;

            var view = go.GetComponent<UnitView>();
            if (view == null) view = go.AddComponent<UnitView>();
            view.Init(unit, def);
            _views[unit] = view;
        }

        public UnitView ViewOf(BattleUnit unit) => unit != null && _views.TryGetValue(unit, out var v) ? v : null;

        // ------------------------------------------------------------------ Loop

        public void SubmitPlayerAction(BattleAction action)
        {
            if (_busy || _battle.State != BattleState.WaitingForPlayer) return;
            if (!_battle.IsActionValid(action, out string error))
            {
                Debug.LogWarning(error);
                hud.ShowCommands(_battle.CurrentUnit);
                return;
            }
            StartCoroutine(Run(_battle.SubmitPlayerAction(action)));
        }

        private IEnumerator Run(List<BattleEvent> events)
        {
            _busy = true;
            for (int i = 0; i < events.Count; i++)
            {
                // Os números da HUD avançam junto com cada evento (cura aparece antes do dano seguinte, etc.)
                if (hud != null) { hud.ApplyEvent(events[i]); hud.Refresh(); }
                UpdateDread();
                yield return Handle(events[i], events, i);
            }
            yield return ReturnAway();
            if (hud != null) { hud.SyncToLive(); hud.Refresh(); }
            _busy = false;

            if (_battle.State == BattleState.WaitingForPlayer && hud != null)
            {
                MoveTurnIndicator(_battle.CurrentUnit);
                hud.SetTurn(_battle.CurrentUnit);
                hud.ShowCommands(_battle.CurrentUnit);
            }
        }

        private IEnumerator Handle(BattleEvent e, List<BattleEvent> all, int index)
        {
            var actorView = ViewOf(e.Actor);
            var targetView = ViewOf(e.Target);
            var skillDef = e.Skill != null ? e.Skill.Tag as SkillDefinition : null;
            var sfx = audioManager;

            switch (e.Type)
            {
                case BattleEventType.BattleStarted:
                    hud?.ShowBanner(encounter.title, "Ordo Realitas · Paris, 1900", 1.8f);
                    yield return new WaitForSeconds(1f);
                    yield break;

                case BattleEventType.RoundStarted:
                    yield break;

                case BattleEventType.InitiativeRolled:
                    hud?.Log(e.Message, e.Roll);
                    yield break;

                case BattleEventType.TurnStarted:
                    yield return ReturnAway();
                    MoveTurnIndicator(e.Actor);
                    hud?.SetTurn(e.Actor);
                    if (_battleCamera != null)
                    {
                        if (e.Actor.Team == Team.Heroes && actorView != null) _battleCamera.Focus(actorView.transform.position);
                        else _battleCamera.Wide();
                    }
                    if (e.Actor.Team == Team.Heroes) sfx?.PlaySfx(sfx.turnStart, 0.5f, 0f);
                    yield return new WaitForSeconds(0.15f);
                    yield break;

                case BattleEventType.TurnSkipped:
                    hud?.Log(e.Message, e.Roll);
                    if (actorView != null) DamagePopup.Spawn(actorView, e.Status == StatusType.Stunned ? "Atordoado" : "Paralisado!", infoColor, 4.5f);
                    break;

                case BattleEventType.ActionStarted:
                {
                    yield return ReturnAway();
                    hud?.Log(e.Message, e.Roll);
                    hud?.ShowBanner(e.Skill.Name, skillDef != null ? skillDef.RulesLabel() : e.Skill.DiceLabel, 1f);
                    if (skillDef != null)
                    {
                        sfx?.PlaySfx(skillDef.castSfx);
                        if (actorView != null) SpawnVfx(skillDef.castVfx, actorView.CenterPosition, actorView.transform.rotation);
                    }
                    var firstTarget = FindFirstTarget(all, index, e.Actor);
                    if (actorView != null)
                    {
                        yield return actorView.PlayAction(skillDef, firstTarget);
                        if (skillDef != null && skillDef.moveToTarget && firstTarget != null && firstTarget != actorView) _awayView = actorView;
                    }
                    yield break; // o impacto vem nos próximos eventos
                }

                case BattleEventType.Damage:
                {
                    hud?.Log(e.Message, e.Roll);
                    hud?.Flash(e.Target);
                    if (targetView == null) break;
                    bool selfCost = e.Actor == e.Target;
                    string text = e.IsCritical ? $"{e.Amount}!" : e.Amount.ToString();
                    DamagePopup.Spawn(targetView, text, e.IsCritical ? criticalColor : damageColor, e.IsCritical ? 8f : 6f);
                    if (e.IsWeakness) DamagePopup.Spawn(targetView, "VULNERÁVEL", infoColor, 4f);
                    if (e.IsResisted) DamagePopup.Spawn(targetView, "RESISTENTE", Color.gray, 4f);

                    if (!selfCost)
                    {
                        if (skillDef != null) SpawnVfx(skillDef.hitVfx, targetView.CenterPosition, Quaternion.identity);
                        AudioClip hitClip = skillDef != null && skillDef.hitSfx != null ? skillDef.hitSfx
                                          : e.IsCritical ? sfx?.criticalHit
                                          : e.IsWeakness ? sfx?.weaknessHit : sfx?.genericHit;
                        if (hitClip == null && sfx != null) hitClip = sfx.genericHit;
                        sfx?.PlaySfx(hitClip);
                        if (targetView.Definition != null) sfx?.PlaySfx(targetView.Definition.hurtSfx, 0.8f);
                        float shake = (skillDef != null ? skillDef.cameraShake : 0.15f) + (e.IsCritical ? 0.25f : 0f);
                        cameraShake?.Shake(shake);
                        if (e.IsCritical && e.Target.Team == Team.Heroes) sanityFx?.Pulse(0.3f);
                    }
                    yield return targetView.PlayHit(e.IsCritical || e.IsWeakness);
                    break;
                }

                case BattleEventType.Heal:
                    hud?.Log(e.Message, e.Roll);
                    hud?.Flash(e.Target);
                    if (targetView != null)
                    {
                        DamagePopup.Spawn(targetView, "+" + e.Amount, healColor, 6f);
                        if (skillDef != null) SpawnVfx(skillDef.hitVfx, targetView.CenterPosition, Quaternion.identity);
                        sfx?.PlaySfx(skillDef != null && skillDef.hitSfx != null ? skillDef.hitSfx : sfx.heal);
                        yield return targetView.PlayHealed(new Color(0.6f, 1f, 0.7f));
                    }
                    break;

                case BattleEventType.SanityChanged:
                    hud?.Log(e.Message, e.Roll);
                    hud?.Flash(e.Target);
                    if (targetView != null && e.Amount != 0)
                    {
                        string text = e.Amount > 0 ? $"+{e.Amount} SAN" : $"{e.Amount} SAN";
                        DamagePopup.Spawn(targetView, text, sanityColor, 5f);
                        if (e.Amount < 0)
                        {
                            sfx?.PlaySfx(sfx.sanityLoss, 0.8f);
                            if (e.Target.Team == Team.Heroes) sanityFx?.Pulse(0.45f);
                            if (e.Skill != null && e.Skill.Element == Element.Fear)
                            {
                                if (skillDef != null) SpawnVfx(skillDef.hitVfx, targetView.CenterPosition, Quaternion.identity);
                                yield return targetView.PlayHit(false);
                            }
                        }
                        else if (skillDef != null)
                        {
                            SpawnVfx(skillDef.hitVfx, targetView.CenterPosition, Quaternion.identity);
                            sfx?.PlaySfx(skillDef.hitSfx != null ? skillDef.hitSfx : sfx.heal);
                        }
                    }
                    break;

                case BattleEventType.Miss:
                    hud?.Log(e.Message, e.Roll);
                    hud?.Flash(e.Target);
                    if (targetView != null) DamagePopup.Spawn(targetView, "ERROU", Color.gray, 5f);
                    sfx?.PlaySfx(sfx.miss);
                    break;

                case BattleEventType.StatusApplied:
                    hud?.Log(e.Message, e.Roll);
                    hud?.Flash(e.Target);
                    if (targetView != null) DamagePopup.Spawn(targetView, BattleSystem.StatusName(e.Status).ToUpperInvariant(), infoColor, 4.5f);
                    sfx?.PlaySfx(sfx.statusApplied, 0.7f);
                    break;

                case BattleEventType.StatusTick:
                    hud?.Log(e.Message, e.Roll);
                    hud?.Flash(e.Target);
                    if (targetView != null)
                    {
                        bool harmful = e.Amount < 0;
                        DamagePopup.Spawn(targetView, harmful ? e.Amount.ToString() : "+" + e.Amount, harmful ? criticalColor : healColor, 5f);
                        if (harmful) yield return targetView.PlayHit(false);
                    }
                    break;

                case BattleEventType.StatusExpired:
                case BattleEventType.ItemUsed:
                    hud?.Log(e.Message, e.Roll);
                    yield break;

                case BattleEventType.Defending:
                    hud?.Log(e.Message, e.Roll);
                    actorView?.PlayDefend();
                    if (actorView != null) DamagePopup.Spawn(actorView, $"+{BattleUnit.DefendBonus} DEF", infoColor, 4.5f);
                    break;

                case BattleEventType.UnitDied:
                    hud?.Log(e.Message, e.Roll);
                    if (actorView != null)
                    {
                        if (actorView.Definition != null) sfx?.PlaySfx(actorView.Definition.deathSfx != null ? actorView.Definition.deathSfx : sfx.death);
                        if (turnIndicator != null)
                        {
                            Vector3 d = turnIndicator.transform.position - actorView.transform.position;
                            if (new Vector2(d.x, d.z).sqrMagnitude < 0.05f) turnIndicator.SetActive(false);
                        }
                        if (actorView == _awayView) _awayView = null;
                        yield return actorView.PlayDeath();
                    }
                    break;

                case BattleEventType.Victory:
                case BattleEventType.Defeat:
                {
                    yield return ReturnAway();
                    bool victory = e.Type == BattleEventType.Victory;
                    if (_battleCamera != null) _battleCamera.Wide();
                    if (turnIndicator != null) turnIndicator.SetActive(false);
                    if (sfx != null)
                    {
                        sfx.StopMusic(0.8f);
                        sfx.PlaySfx(victory ? encounter.victoryStinger : encounter.defeatStinger, 1f, 0f);
                    }
                    hud?.SetTurn(null);
                    yield return new WaitForSeconds(0.8f);
                    hud?.ShowEnd(victory, e.Message);
                    yield break;
                }
            }

            yield return new WaitForSeconds(pauseBetweenEvents);
        }

        private UnitView FindFirstTarget(List<BattleEvent> all, int from, BattleUnit actor)
        {
            for (int i = from + 1; i < all.Count; i++)
            {
                var ev = all[i];
                if (ev.Type == BattleEventType.ActionStarted || ev.Type == BattleEventType.TurnStarted) break;
                if (ev.Target != null && ev.Target != actor) return ViewOf(ev.Target);
            }
            return null;
        }

        private IEnumerator ReturnAway()
        {
            if (_awayView == null) yield break;
            var view = _awayView;
            _awayView = null;
            yield return view.ReturnHome();
        }

        private void MoveTurnIndicator(BattleUnit unit)
        {
            if (turnIndicator == null) return;
            var view = ViewOf(unit);
            if (view == null || !unit.IsAlive) { turnIndicator.SetActive(false); return; }
            turnIndicator.SetActive(true);
            var p = view.transform.position;
            turnIndicator.transform.position = new Vector3(p.x, turnIndicator.transform.position.y, p.z);
        }

        private void UpdateDread()
        {
            if (sanityFx == null) return;
            var alive = _battle.Heroes.Where(h => hud != null ? hud.Shown(h).Alive : h.IsAlive).ToList();
            float avg = alive.Count == 0 ? 0f : alive.Average(h =>
            {
                if (hud == null || h.Stats.MaxSanity <= 0) return h.SanityPercent;
                return Mathf.Clamp01((float)hud.Shown(h).Sanity / h.Stats.MaxSanity);
            });
            sanityFx.SetDread(1f - avg);
        }

        private static void SpawnVfx(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null) return;
            var go = Instantiate(prefab, position, rotation);
            Destroy(go, 4f);
        }
    }
}
