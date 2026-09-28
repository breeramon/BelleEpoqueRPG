using System;
using UnityEngine;

namespace BelleEpoque
{
    /// <summary>
    /// Câmera da batalha. No turno de um agente, vai para trás do ombro dele olhando as ameaças
    /// (como no Expedition 33). No turno das ameaças, volta para o plano aberto que mostra todo mundo.
    /// Fica num "rig" pai da Main Camera, para o tremor de câmera continuar funcionando.
    /// </summary>
    public class BattleCamera : MonoBehaviour
    {
        [Header("Plano do agente")]
        [SerializeField] private float distanceBehind = 3.4f;
        [SerializeField] private float sideOffset = 1.4f;
        [SerializeField] private float height = 2.0f;
        [Tooltip("0 = olha para o agente, 1 = olha para as ameaças")]
        [Range(0f, 1f)] [SerializeField] private float lookBias = 0.55f;

        [Header("Suavidade")]
        [SerializeField] private float moveTime = 0.45f;
        [SerializeField] private float turnSpeed = 5f;

        private Vector3 _wideP, _targetP, _velocity;
        private Quaternion _wideR, _targetR;
        private Func<Vector3> _enemyCenter;

        public void Init(Func<Vector3> enemyCenter)
        {
            _enemyCenter = enemyCenter;
            _wideP = _targetP = transform.position;
            _wideR = _targetR = transform.rotation;
        }

        /// <summary>Plano aberto, com todos os personagens.</summary>
        public void Wide()
        {
            _targetP = _wideP;
            _targetR = _wideR;
        }

        /// <summary>Por cima do ombro do agente, olhando para as ameaças.</summary>
        public void Focus(Vector3 heroPosition)
        {
            Vector3 enemies = _enemyCenter != null ? _enemyCenter() : heroPosition + Vector3.forward * 6f;
            Vector3 dir = enemies - heroPosition;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
            dir.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, dir);

            _targetP = heroPosition - dir * distanceBehind + right * sideOffset + Vector3.up * height;
            Vector3 look = Vector3.Lerp(heroPosition, enemies, lookBias) + Vector3.up * 1.1f;
            _targetR = Quaternion.LookRotation(look - _targetP);
        }

        [Header("Plano de escolha de alvo (mais alto, mostra as ameaças)")]
        [SerializeField] private float targetDistanceBehind = 5.2f;
        [SerializeField] private float targetHeight = 4.2f;
        [Range(0f, 1f)] [SerializeField] private float targetLookBias = 0.85f;

        /// <summary>Sobe e recua a câmera atrás do agente para mostrar todas as ameaças e o alvo em foco.</summary>
        public void FocusTargets(Vector3 heroPosition)
        {
            Vector3 enemies = _enemyCenter != null ? _enemyCenter() : heroPosition + Vector3.forward * 6f;
            Vector3 dir = enemies - heroPosition;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
            dir.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, dir);

            _targetP = heroPosition - dir * targetDistanceBehind + right * (sideOffset * 0.6f) + Vector3.up * targetHeight;
            Vector3 look = Vector3.Lerp(heroPosition, enemies, targetLookBias) + Vector3.up * 0.6f;
            _targetR = Quaternion.LookRotation(look - _targetP);
        }

        private void LateUpdate()
        {
            transform.position = Vector3.SmoothDamp(transform.position, _targetP, ref _velocity, moveTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, _targetR, 1f - Mathf.Exp(-turnSpeed * Time.deltaTime));
        }
    }
}
