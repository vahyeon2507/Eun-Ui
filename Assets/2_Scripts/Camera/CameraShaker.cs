// using System.Collections;
// using UnityEngine;
// using Unity.Cinemachine;                  // CM3

// /// <summary>
// /// CM3 전용 카메라 쉐이커.
// /// - Active VCam(지정 없으면 Brain의 LiveCamera)을 찾아 Position/Rotation Noise의 Amplitude를 잠시 올렸다가 0으로 복귀.
// /// - 흔드는 동안 Follow Damping을 0으로 낮췄다가 복구(노이즈가 죽지 않도록).
// /// - VCam에 Position/Rotation Noise가 없으면 자동으로 붙여줌.
// /// - VCam이 없으면 Transform 기반 fallback(아주 간단한 좌우 흔들) 사용.
// /// </summary>
// [DisallowMultipleComponent]
// public class CameraShaker : MonoBehaviour
// {
//     [Header("Wiring (optional)")]
//     public CinemachineBrain brain;                // 비워두면 Camera.main에서 자동
//     public CinemachineCamera explicitVcam;        // 비워두면 Brain의 LiveCamera 사용

//     [Header("Defaults")]
//     public float defaultDuration = 0.18f;
//     public float defaultPosAmp = 0.35f;  // Position Noise amplitude
//     public float defaultRotAmp = 0.35f;  // Rotation Noise amplitude
//     public float defaultFreq = 1.5f;   // Frequency Gain
//     public bool useUnscaledTime = true;

//     // 내부 상태 저장(댐핑 복구용)
//     Vector3 _savedDamping = Vector3.zero;
//     bool _dampingSaved = false;

//     // === 외부에서 호출 ===
//     public void Shake(float duration) =>
//         Shake(duration, defaultPosAmp, defaultRotAmp, defaultFreq);

//     public void Shake(float duration, float posAmp, float rotAmp, float freq)
//     {
//         var vcam = ResolveLiveVcam();
//         if (vcam != null)
//         {
//             StopAllCoroutines();
//             StartCoroutine(ShakeCM3(vcam, duration, posAmp, rotAmp, freq));
//         }
//         else
//         {
//             // Fallback: 진짜 VCam이 없으면 Transform을 살짝 흔듦
//             StopAllCoroutines();
//             StartCoroutine(ShakeTransformFallback(duration, 0.25f));
//         }
//     }

// #if UNITY_EDITOR
//     // 컴포넌트 우측 점3개 메뉴에서 바로 테스트 가능
//     [ContextMenu("Test/Light")] void TestLight() => Shake(defaultDuration, 0.25f, 0.20f, 1.2f);
//     [ContextMenu("Test/Heavy")] void TestHeavy() => Shake(0.35f, 0.70f, 0.55f, 2.0f);
//     [ContextMenu("Test/Boss")] void TestBoss() => Shake(0.55f, 1.10f, 0.90f, 2.3f);
// #endif

//     // === CM3 루틴 ===
//     IEnumerator ShakeCM3(CinemachineCamera vcam, float duration, float posAmp, float rotAmp, float freq)
//     {
//         // 1) 필요한 노이즈/바디 컴포넌트 확보(없으면 추가)
//         var posNoise = vcam.GetComponent<CinemachinePositionNoise>();
//         if (!posNoise) posNoise = vcam.gameObject.AddComponent<CinemachinePositionNoise>();

//         var rotNoise = vcam.GetComponent<CinemachineRotationNoise>();
//         if (!rotNoise) rotNoise = vcam.gameObject.AddComponent<CinemachineRotationNoise>();

//         var follow = vcam.GetComponent<CinemachineFollow>(); // Body
//         TrySaveAndZeroDamping(follow, true);                 // 흔드는 동안 Damping 을 0으로

//         // 2) 주파수 설정
//         posNoise.FrequencyGain = freq;
//         rotNoise.FrequencyGain = freq;

//         // 3) 진폭 올리기
//         posNoise.AmplitudeGain = posAmp;
//         rotNoise.AmplitudeGain = rotAmp;

//         float t = 0f;
//         while (t < duration)
//         {
//             t += GetDt();
//             yield return null;
//         }

//         // 4) 진폭 0으로 원복
//         posNoise.AmplitudeGain = 0f;
//         rotNoise.AmplitudeGain = 0f;

//         // 5) 댐핑 복구
//         TrySaveAndZeroDamping(follow, false);
//     }

//     // Follow Damping을 0으로 내렸다가 복구
//     void TrySaveAndZeroDamping(CinemachineFollow follow, bool toZero)
//     {
//         if (!follow) return;

//         if (toZero)
//         {
//             if (!_dampingSaved)
//             {
//                 _savedDamping = follow.Damping;
//                 _dampingSaved = true;
//             }
//             follow.Damping = Vector3.zero;
//         }
//         else
//         {
//             if (_dampingSaved)
//             {
//                 follow.Damping = _savedDamping;
//                 _dampingSaved = false;
//             }
//         }
//     }

//     // === Transform fallback (VCam 없을 때) ===
//     IEnumerator ShakeTransformFallback(float duration, float strength)
//     {
//         var cam = Camera.main ? Camera.main.transform : transform;
//         Vector3 basePos = cam.localPosition;
//         float t = 0f;
//         while (t < duration)
//         {
//             t += GetDt();
//             cam.localPosition = basePos + (Vector3)Random.insideUnitCircle * strength;
//             yield return null;
//         }
//         cam.localPosition = basePos;
//     }

//     float GetDt() => useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

//     // 현재 라이브 VCam 찾아오기
//     CinemachineCamera ResolveLiveVcam()
//     {
//         if (explicitVcam) return explicitVcam;

//         var b = brain;
//         if (!b) b = Camera.main ? Camera.main.GetComponent<CinemachineBrain>() : null;
//         if (!b) return null;

//         return b.ActiveVirtualCamera as CinemachineCamera; // CM3
//     }
// }
