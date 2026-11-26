using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using VRRehab.Core;
using VRRehab.Persistence;

namespace VRRehab.Interactive.Task16
{
    /// <summary>
    /// Estante de troféus do Menu Principal (TASK-16, retoque).
    ///
    /// Correção do "teleporte": a versão antiga tocava o som de pegar movendo o transform do AudioSource — que era a
    /// RAIZ da estante — até o troféu, arrastando base, rótulos e os outros troféus. Agora o som sai de um filho
    /// dedicado (TrophyAudio) e NADA neste script escreve na pose da raiz, das âncoras, da base ou das placas.
    ///
    /// Responsabilidades: estado bloqueado/conquistado (silhueta + placa com cadeado), placa "Coroa no Drone",
    /// cartão de informação único (laser/segurar), som de pegar/encaixar, brilho do anel ao encaixar, giro lento dos
    /// troféus e pulso dos halos (um único loop, só com o jogador a ≤ 4 m), fanfarra de desbloqueio.
    /// O retorno/encaixe no pedestal é da ShelfTrophyGrabInteractable de cada troféu.
    /// </summary>
    public class TrophyShelfManager : MonoBehaviour
    {
        public enum ProgressKind { None, Apples, Pinecones, BasketsAndCoins, Steps }

        [System.Serializable]
        public class TrophySlot
        {
            public string trophyId;
            public string title;
            [TextArea] public string description;
            public GameObject trophyObject;
            public Transform slotAnchor;
            public TextMeshPro plaqueText;
            public ParticleSystem unlockParticles;
            [Tooltip("Ícone de cadeado na placa (aparece só bloqueado).")]
            public Renderer plaqueLockIcon;
            [Tooltip("Anel emissivo embutido no pedestal.")]
            public Renderer ringRenderer;
            [Tooltip("Halo radial atrás do troféu.")]
            public Renderer haloRenderer;
            public Color accentColor = new Color(1f, 0.8f, 0.3f);
            public ProgressKind progress = ProgressKind.None;

            [HideInInspector] public Vector3 initialLocalPos;
            [HideInInspector] public Quaternion initialLocalRot;

            [System.NonSerialized] public ShelfTrophyGrabInteractable grab;
            [System.NonSerialized] public Renderer[] renderers;
            [System.NonSerialized] public Material[][] originalMaterials;
            [System.NonSerialized] public bool unlocked = true;
            [System.NonSerialized] public bool onDrone;
            [System.NonSerialized] public int stateKey = -1;
            [System.NonSerialized] public float spinAngle;
            [System.NonSerialized] public float ringFlash;
            [System.NonSerialized] public int hoverCount;
            [System.NonSerialized] public DroneCrownWearable crown;
        }

        [Header("Slots dos Troféus")]
        public List<TrophySlot> slots = new List<TrophySlot>();

        [Header("Efeitos Sonoros")]
        public AudioClip sfxUnlockFanfare;
        public AudioClip sfxPickupShimmer;
        public AudioClip sfxPlaceClick;
        [Tooltip("Fonte de áudio FILHA (nunca a raiz). Criada no Awake se faltar.")]
        public AudioSource trophyAudio;
        [Range(0f, 1f)] public float sfxVolume = 0.5f;

        [Header("Retorno ao pedestal")]
        [Tooltip("Troféu solto longe do pedestal volta sozinho depois deste tempo (s).")]
        public float autoRespawnDelay = 1.5f;

        [Header("Bloqueado")]
        [Tooltip("Material único de silhueta para troféus ainda não conquistados.")]
        public Material lockedSilhouetteMaterial;
        public Color lockedTextColor = new Color(0.55f, 0.57f, 0.62f);
        public Color unlockedTextColor = new Color(1f, 0.86f, 0.45f);

        [Header("Cartão de informação (um só, compartilhado)")]
        public Transform infoCard;
        public TextMeshPro infoTitle;
        public TextMeshPro infoBody;
        [Tooltip("Posição do cartão em relação ao troféu, no referencial da estante (y = cima, z = frente). " +
                 "Na prateleira da sala só há 0,30 m até a tábua de cima, então o cartão fica à frente e um pouco acima.")]
        public Vector3 infoCardOffset = new Vector3(0f, 0.12f, 0.22f);
        public float infoCardHideDelay = 0.3f;

        [Header("Idle (barato)")]
        public float idleSpinDegreesPerSecond = 12f;
        public float activeRadius = 4f;
        public float haloPulseSpeed = 1.6f;

        // ------------------------------------------------------------------ estado
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        private MaterialPropertyBlock mpb;
        private Transform viewer;
        private float viewerSearchTimer;
        private TrophySlot cardSlot;
        private float cardHideAt = -1f;
        private readonly Dictionary<IXRInteractable, TrophySlot> byInteractable = new Dictionary<IXRInteractable, TrophySlot>();

        public bool InfoCardVisible => infoCard != null && infoCard.gameObject.activeSelf;
        public string InfoCardTitle => infoTitle != null ? infoTitle.text : "";
        public TrophySlot GetSlot(string id) { foreach (var s in slots) if (s.trophyId == id) return s; return null; }

        // ------------------------------------------------------------------ ciclo de vida
        private void Awake()
        {
            mpb = new MaterialPropertyBlock();
            if (trophyAudio == null || trophyAudio.gameObject == gameObject)
            {
                Transform t = transform.Find("TrophyAudio");
                if (t == null)
                {
                    t = new GameObject("TrophyAudio").transform;
                    t.SetParent(transform, false);
                }
                trophyAudio = t.GetComponent<AudioSource>() ?? t.gameObject.AddComponent<AudioSource>();
            }
            trophyAudio.playOnAwake = false;
            trophyAudio.loop = false;
            trophyAudio.spatialBlend = 1f;
            trophyAudio.minDistance = 0.5f;
            trophyAudio.maxDistance = 8f;

            if (infoCard != null) infoCard.gameObject.SetActive(false);

            foreach (var slot in slots)
            {
                if (slot.trophyObject == null && slot.slotAnchor != null && slot.slotAnchor.childCount > 0)
                    slot.trophyObject = slot.slotAnchor.GetChild(0).gameObject;
                if (slot.trophyObject == null) continue;

                slot.initialLocalPos = slot.trophyObject.transform.localPosition;
                slot.initialLocalRot = slot.trophyObject.transform.localRotation;
                slot.renderers = slot.trophyObject.GetComponentsInChildren<Renderer>(true);
                slot.originalMaterials = new Material[slot.renderers.Length][];
                for (int i = 0; i < slot.renderers.Length; i++) slot.originalMaterials[i] = slot.renderers[i].sharedMaterials;

                slot.grab = slot.trophyObject.GetComponent<ShelfTrophyGrabInteractable>();
                if (slot.grab != null)
                {
                    slot.grab.ReturnDelaySeconds = autoRespawnDelay;
                    byInteractable[slot.grab] = slot;
                    slot.grab.hoverEntered.AddListener(OnTrophyHoverEntered);
                    slot.grab.hoverExited.AddListener(OnTrophyHoverExited);
                    slot.grab.selectEntered.AddListener(OnTrophySelectEntered);
                    slot.grab.selectExited.AddListener(OnTrophySelectExited);
                    slot.grab.Docked += OnTrophyDocked;
                }
                slot.crown = slot.trophyObject.GetComponent<DroneCrownWearable>();
                if (slot.crown != null) slot.crown.EquipChanged += OnCrownEquipChanged;
                if (slot.unlockParticles != null) slot.unlockParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private void Start()
        {
            if (RehabProgressionSaveSystem.Instance != null)
            {
                RehabProgressionSaveSystem.Instance.OnSaveDataUpdated += RefreshShelfState;
                RehabProgressionSaveSystem.Instance.OnTrophyUnlocked += HandleTrophyUnlocked;
                RefreshShelfState(RehabProgressionSaveSystem.Instance.Data);
            }
            else RefreshShelfState(null);
            FindViewer();
        }

        private void OnDestroy()
        {
            if (RehabProgressionSaveSystem.Instance != null)
            {
                RehabProgressionSaveSystem.Instance.OnSaveDataUpdated -= RefreshShelfState;
                RehabProgressionSaveSystem.Instance.OnTrophyUnlocked -= HandleTrophyUnlocked;
            }
            foreach (var slot in slots)
            {
                if (slot.grab != null)
                {
                    slot.grab.hoverEntered.RemoveListener(OnTrophyHoverEntered);
                    slot.grab.hoverExited.RemoveListener(OnTrophyHoverExited);
                    slot.grab.selectEntered.RemoveListener(OnTrophySelectEntered);
                    slot.grab.selectExited.RemoveListener(OnTrophySelectExited);
                    slot.grab.Docked -= OnTrophyDocked;
                }
                if (slot.crown != null) slot.crown.EquipChanged -= OnCrownEquipChanged;
            }
        }

        // ------------------------------------------------------------------ estado (só quando muda)
        public void RefreshShelfState(RehabSaveData data)
        {
            foreach (var slot in slots)
            {
                if (slot.trophyObject == null) continue;
                slot.unlocked = RehabProgressionSaveSystem.Instance == null || RehabProgressionSaveSystem.Instance.IsTrophyUnlocked(slot.trophyId);
                slot.onDrone = slot.crown != null && slot.crown.isEquippedOnDrone;
                ApplySlotState(slot);
            }
        }

        private void ApplySlotState(TrophySlot slot)
        {
            int key = slot.unlocked ? (slot.onDrone ? 2 : 1) : 0;
            if (key == slot.stateKey) return;
            slot.stateKey = key;

            // Troféu: silhueta quando bloqueado (troca de sharedMaterials, sem instanciar material)
            if (slot.renderers != null)
            {
                for (int i = 0; i < slot.renderers.Length; i++)
                {
                    Renderer r = slot.renderers[i];
                    if (r == null || r is ParticleSystemRenderer) continue;
                    if (slot.unlocked || lockedSilhouetteMaterial == null) r.sharedMaterials = slot.originalMaterials[i];
                    else
                    {
                        var mats = new Material[slot.originalMaterials[i].Length];
                        for (int m = 0; m < mats.Length; m++) mats[m] = lockedSilhouetteMaterial;
                        r.sharedMaterials = mats;
                    }
                }
            }
            if (slot.grab != null)
            {
                if (!slot.unlocked)
                {
                    if (slot.grab.isSelected && slot.grab.interactionManager != null)
                        slot.grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)slot.grab);
                    slot.grab.SnapHomeInstant();
                }
                slot.grab.enabled = slot.unlocked;        // bloqueado = não pegável
            }

            if (slot.plaqueText != null)
            {
                slot.plaqueText.text = !slot.unlocked ? "Bloqueado" : slot.onDrone ? "Coroa no Drone" : slot.title;
                slot.plaqueText.color = slot.unlocked ? unlockedTextColor : lockedTextColor;
            }
            if (slot.plaqueLockIcon != null) slot.plaqueLockIcon.enabled = !slot.unlocked;
            if (slot.haloRenderer != null) slot.haloRenderer.enabled = slot.unlocked && !slot.onDrone;
            SetRing(slot, slot.unlocked && !slot.onDrone ? 1f : 0.08f);
        }

        private void OnCrownEquipChanged(bool equipped)
        {
            foreach (var slot in slots)
            {
                if (slot.crown == null) continue;
                slot.onDrone = equipped;
                ApplySlotState(slot);
            }
        }

        private void HandleTrophyUnlocked(string trophyId)
        {
            TrophySlot slot = GetSlot(trophyId);
            if (slot == null || slot.trophyObject == null) return;
            slot.unlocked = true;
            ApplySlotState(slot);
            if (slot.unlockParticles != null) slot.unlockParticles.Play();
            PlayAt(sfxUnlockFanfare, slot.trophyObject.transform.position, 0.9f);
            slot.ringFlash = 1f;
        }

        // ------------------------------------------------------------------ eventos dos troféus
        private void OnTrophyHoverEntered(HoverEnterEventArgs args)
        {
            if (!byInteractable.TryGetValue(args.interactableObject, out TrophySlot slot)) return;
            slot.hoverCount++;
            ShowCard(slot);
        }

        private void OnTrophyHoverExited(HoverExitEventArgs args)
        {
            if (!byInteractable.TryGetValue(args.interactableObject, out TrophySlot slot)) return;
            slot.hoverCount = Mathf.Max(0, slot.hoverCount - 1);
            if (slot == cardSlot && slot.hoverCount == 0 && !(slot.grab != null && slot.grab.isSelected)) cardHideAt = Time.time + infoCardHideDelay;
        }

        private void OnTrophySelectEntered(SelectEnterEventArgs args)
        {
            if (!byInteractable.TryGetValue(args.interactableObject, out TrophySlot slot)) return;
            PlayAt(sfxPickupShimmer, slot.trophyObject.transform.position, 0.8f);
            ShowCard(slot);
        }

        private void OnTrophySelectExited(SelectExitEventArgs args)
        {
            if (!byInteractable.TryGetValue(args.interactableObject, out TrophySlot slot)) return;
            if (slot == cardSlot && slot.hoverCount == 0) cardHideAt = Time.time + infoCardHideDelay;
        }

        private void OnTrophyDocked(ShelfTrophyGrabInteractable grab, bool snapped)
        {
            if (!byInteractable.TryGetValue(grab, out TrophySlot slot)) return;
            slot.spinAngle = 0f;
            slot.ringFlash = 1f;
            PlayAt(sfxPlaceClick, grab.transform.position, snapped ? 0.7f : 0.55f);
        }

        // ------------------------------------------------------------------ cartão de informação
        private void ShowCard(TrophySlot slot)
        {
            if (infoCard == null || slot == null) return;
            cardHideAt = -1f;
            if (cardSlot != slot)
            {
                cardSlot = slot;
                if (infoTitle != null) infoTitle.text = slot.unlocked ? slot.title : "Troféu bloqueado";
                if (infoBody != null) infoBody.text = BuildBody(slot);       // string montada só ao trocar de troféu
            }
            if (!infoCard.gameObject.activeSelf) infoCard.gameObject.SetActive(true);
            PlaceCard();
        }

        private string BuildBody(TrophySlot slot)
        {
            string body = slot.description;
            var data = RehabProgressionSaveSystem.Instance != null ? RehabProgressionSaveSystem.Instance.Data : null;
            if (data == null) return body;
            switch (slot.progress)
            {
                case ProgressKind.Apples: return body + $"\n<color=#FFE08A>Maçãs colhidas: {data.totalApplesHarvested}</color>";
                case ProgressKind.Pinecones: return body + $"\n<color=#FFE08A>Sementes plantadas: {data.totalPineconesPlanted}</color>";
                case ProgressKind.BasketsAndCoins: return body + $"\n<color=#FFE08A>Cestas: {data.totalBasketsScored} · Moedas: {data.totalCoinsCollected}</color>";
                case ProgressKind.Steps: return body + $"\n<color=#FFE08A>Passos dados: {data.totalStepsTaken}</color>";
                default: return body;
            }
        }

        private void PlaceCard()
        {
            if (infoCard == null || cardSlot == null || cardSlot.trophyObject == null) return;
            Vector3 p = cardSlot.trophyObject.transform.position + transform.up * infoCardOffset.y
                        + transform.forward * infoCardOffset.z + transform.right * infoCardOffset.x;
            Quaternion r = infoCard.rotation;
            if (viewer != null)
            {
                Vector3 d = p - viewer.position;
                if (d.sqrMagnitude > 1e-4f) r = Quaternion.LookRotation(d, Vector3.up);   // billboard sem roll
            }
            infoCard.SetPositionAndRotation(p, r);
        }

        // ------------------------------------------------------------------ loop único (idle + cartão)
        private void Update()
        {
            if (viewer == null)
            {
                viewerSearchTimer -= Time.deltaTime;
                if (viewerSearchTimer <= 0f) FindViewer();
            }

            if (InfoCardVisible)
            {
                if (cardHideAt > 0f && Time.time >= cardHideAt) { infoCard.gameObject.SetActive(false); cardHideAt = -1f; }
                else PlaceCard();
            }

            if (viewer != null && (viewer.position - transform.position).sqrMagnitude > activeRadius * activeRadius) return;

            float dt = Time.deltaTime;
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * haloPulseSpeed);
            for (int i = 0; i < slots.Count; i++)
            {
                TrophySlot s = slots[i];
                if (s.trophyObject == null || !s.unlocked || s.onDrone) continue;
                var g = s.grab;
                if (g != null && g.IsDocked && !g.isSelected && !g.IsReturningHome)
                {
                    s.spinAngle = Mathf.Repeat(s.spinAngle + idleSpinDegreesPerSecond * dt, 360f);
                    s.trophyObject.transform.rotation = g.HomeRotation * Quaternion.Euler(0f, s.spinAngle, 0f);
                }
                if (s.haloRenderer != null)
                {
                    s.haloRenderer.GetPropertyBlock(mpb);
                    Color c = s.accentColor; c.a = 0.55f * pulse;
                    mpb.SetColor(ColorId, c);
                    s.haloRenderer.SetPropertyBlock(mpb);
                }
                if (s.ringFlash > 0f)
                {
                    s.ringFlash = Mathf.Max(0f, s.ringFlash - dt * 1.6f);
                    SetRing(s, 1f + 2.5f * s.ringFlash);
                }
            }
        }

        private void SetRing(TrophySlot slot, float intensity)
        {
            if (slot.ringRenderer == null) return;
            slot.ringRenderer.GetPropertyBlock(mpb);
            mpb.SetColor(EmissionId, slot.accentColor * intensity);
            slot.ringRenderer.SetPropertyBlock(mpb);
        }

        private void FindViewer()
        {
            viewerSearchTimer = 1f;
            Camera cam = Camera.main;
            if (cam != null) viewer = cam.transform;
        }

        private void PlayAt(AudioClip clip, Vector3 position, float volume)
        {
            if (clip == null || trophyAudio == null) return;
            // Move só o FILHO de áudio (nunca a raiz da estante)
            trophyAudio.transform.position = position;
            trophyAudio.pitch = Random.Range(0.96f, 1.04f);
            trophyAudio.PlayOneShot(clip, volume * sfxVolume * 2f);
        }

        /// <summary>Força o retorno suave de todos os troféus aos seus pedestais.</summary>
        public void RespawnAllTrophies()
        {
            foreach (var slot in slots)
                if (slot.grab != null && slot.unlocked && !slot.onDrone) slot.grab.TriggerManualRespawn();
        }
    }
}
