using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using VRRehab.Core;
using VRRehab.DronePet;
using VRRehab.Ergonomics;

namespace VRRehab.Interactive.Task11
{
    /// <summary>
    /// Cesta de Coleta de Maçãs em Vime Trançado (TASK-11).
    /// Implanta IAimAssistTarget para atração e correção de mira pelo ThrowAimAssist,
    /// detecta acertos de maçãs vermelhas (+10) e douradas (+50),
    /// dispara sinos festivos, popup 3D elástico e partículas de confetes.
    /// </summary>
    [DisallowMultipleComponent]
    public class AppleCollectionBasket : MonoBehaviour, IAimAssistTarget
    {
        [Header("Pontuação e Metas")]
        [SerializeField] private int totalScore = 0;
        [SerializeField] private int applesScoredCount = 0;

        [Header("Efeitos e Áudio")]
        [SerializeField] private AudioClip sfxScoreBell;
        [SerializeField] private ParticleSystem confettiFx;
        [SerializeField] private TextMeshPro scoreboardText;

        [Header("Dimensões do Sensor de Acerto")]
        [SerializeField] private Vector3 rimCenterOffset = new Vector3(0f, 0.38f, 0f);
        [SerializeField] private float rimRadius = 0.25f;

        // IAimAssistTarget
        public Vector3 AimTargetPosition => transform.position + rimCenterOffset;
        public float AimTargetRadius => rimRadius;
        public bool IsAimTargetActive => true;
        public Transform TargetTransform => transform;

        public int TotalScore => totalScore;
        public int ApplesScoredCount => applesScoredCount;

        public static event System.Action<int, int> OnAppleScoredEvent; // pointsEarned, totalApplesScored

        private HashSet<InteractiveApple> scoredApples = new HashSet<InteractiveApple>();
        private DroneMilestoneCelebration droneCelebration;

        private void Awake()
        {
            // Garante trigger de detecção na boca da cesta
            SphereCollider mouthTrigger = GetComponent<SphereCollider>();
            if (mouthTrigger == null)
            {
                mouthTrigger = gameObject.AddComponent<SphereCollider>();
            }
            mouthTrigger.isTrigger = true;
            mouthTrigger.center = rimCenterOffset;
            mouthTrigger.radius = rimRadius;

            UpdateScoreboardDisplay();
        }

        private void OnEnable()
        {
            AimAssistTargetRegistry.Register(this);
            droneCelebration = FindFirstObjectByType<DroneMilestoneCelebration>();
        }

        private void OnDisable()
        {
            AimAssistTargetRegistry.Unregister(this);
        }

        private void OnTriggerEnter(Collider other)
        {
            InteractiveApple apple = other.GetComponentInParent<InteractiveApple>();
            if (apple == null) return;
            if (scoredApples.Contains(apple)) return;

            // Registra para não pontuar duas vezes a mesma maçã
            scoredApples.Add(apple);

            // Calcula pontos
            int pointsEarned = apple.ScoreValue;
            totalScore += pointsEarned;
            applesScoredCount++;
            OnAppleScoredEvent?.Invoke(pointsEarned, applesScoredCount);

            // 1. Áudio alegre de sino de pontuação
            if (sfxScoreBell != null)
            {
                VRFeedbackManager.Instance?.PlaySound(sfxScoreBell, AimTargetPosition, 0.95f, 0.98f, 1.02f);
            }

            // 2. Confetes festivos na cesta
            if (confettiFx != null)
            {
                confettiFx.transform.position = AimTargetPosition;
                confettiFx.Play();
            }

            // 3. Popup 3D Flutuante ("+10 PTS" / "+50 PTS")
            SpawnScorePopup(pointsEarned, apple.Type == AppleType.GoldenBonus);

            // 4. Atualiza placa de madeira
            UpdateScoreboardDisplay();

            // 5. Integração com Drone Pet (celebração a cada 50 pontos)
            if (totalScore % 50 == 0 && droneCelebration != null)
            {
                droneCelebration.TriggerCelebration(CelebrationCategory.Geral, $"Cesta de Maçãs! Total: {totalScore} pontos!");
            }

            // Desativa agarre da maçã para que ela assente na cesta
            StartCoroutine(SettleAppleInBasketRoutine(apple));
        }

        private IEnumerator SettleAppleInBasketRoutine(InteractiveApple apple)
        {
            Rigidbody rb = apple.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearDamping = 4.0f;
                rb.angularDamping = 4.0f;
            }

            // Após 2.5s reduz a escala suavemente para liberar espaço na cesta
            yield return new WaitForSeconds(2.5f);

            if (apple != null)
            {
                Vector3 originalScale = apple.transform.localScale;
                float elapsed = 0f;
                float duration = 0.6f;
                while (elapsed < duration)
                {
                    if (apple == null) yield break;
                    elapsed += Time.deltaTime;
                    float t = elapsed / duration;
                    apple.transform.localScale = Vector3.Lerp(originalScale, Vector3.zero, t);
                    yield return null;
                }
                Destroy(apple.gameObject);
            }
        }

        private void SpawnScorePopup(int points, bool isGolden)
        {
            GameObject popupGo = new GameObject("ScorePopup3D");
            popupGo.transform.position = AimTargetPosition + Vector3.up * 0.15f;
            popupGo.transform.rotation = Quaternion.LookRotation(popupGo.transform.position - Camera.main.transform.position);

            TextMeshPro tmp = popupGo.AddComponent<TextMeshPro>();
            tmp.text = $"+{points} PTS";
            tmp.fontSize = 4.0f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = isGolden ? new Color(1.0f, 0.85f, 0.15f, 1.0f) : new Color(0.25f, 1.0f, 0.35f, 1.0f);

            StartCoroutine(AnimateScorePopup(popupGo, tmp));
        }

        private IEnumerator AnimateScorePopup(GameObject popupGo, TextMeshPro tmp)
        {
            float duration = 1.4f;
            float elapsed = 0f;
            Vector3 startPos = popupGo.transform.position;
            Vector3 endPos = startPos + Vector3.up * 0.45f;
            Color initialColor = tmp.color;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                popupGo.transform.position = Vector3.Lerp(startPos, endPos, Mathf.SmoothStep(0f, 1f, t));
                // Sempre olha para a câmera VR
                if (Camera.main != null)
                {
                    popupGo.transform.rotation = Quaternion.LookRotation(popupGo.transform.position - Camera.main.transform.position);
                }

                // Elastic scale punch
                float scale = 1.0f + 0.35f * Mathf.Sin(t * Mathf.PI);
                popupGo.transform.localScale = Vector3.one * scale * 0.15f;

                // Fade out no terço final
                if (t > 0.65f)
                {
                    float alpha = (1f - t) / 0.35f;
                    tmp.color = new Color(initialColor.r, initialColor.g, initialColor.b, alpha);
                }

                yield return null;
            }

            Destroy(popupGo);
        }

        private void UpdateScoreboardDisplay()
        {
            if (scoreboardText != null)
            {
                scoreboardText.text = $"<b>CESTA DE MAÇÃS</b>\n<color=#FFD700>{totalScore} PTS</color>\n<size=70%>Colhidas: {applesScoredCount}</size>";
            }
        }

        public void Configure(AudioClip bellSfx, ParticleSystem confetti, TextMeshPro textMesh)
        {
            sfxScoreBell = bellSfx;
            confettiFx = confetti;
            scoreboardText = textMesh;
            UpdateScoreboardDisplay();
        }
    }
}
