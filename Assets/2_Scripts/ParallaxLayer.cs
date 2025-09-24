using UnityEngine;

public class ParallaxLayer : MonoBehaviour
{
    [Header("Camera & Parallax")]
    [SerializeField] private Transform cam;
    [SerializeField, Range(0f, 1f)] private float parallaxEffect = 0.3f;

    [Header("Background Layers")]
    [SerializeField] private Transform centerLayer; // Inspector에서 중앙 레이어 지정

    private Transform leftLayer;
    private Transform rightLayer;
    private float lastCamX;
    private float spriteWidth;

    void Start()
    {
        // 카메라 자동 할당
        if (cam == null)
        {
            if (Camera.main != null) cam = Camera.main.transform;
            else Debug.LogError("No camera found! Assign cam in Inspector.");
        }

        if (centerLayer == null)
        {
            if (transform.childCount > 0) centerLayer = transform.GetChild(0);
            else Debug.LogError("Assign a child sprite as centerLayer!");
        }

        // 좌우 레이어 생성
        leftLayer = Instantiate(centerLayer, centerLayer.position, Quaternion.identity, transform);
        rightLayer = Instantiate(centerLayer, centerLayer.position, Quaternion.identity, transform);

        // 실제 월드 단위 폭 계산 (PPU, 스케일 반영)
        SpriteRenderer sr = centerLayer.GetComponent<SpriteRenderer>();
        spriteWidth = sr.bounds.size.x;

        // 좌우 레이어 위치 초기화
        leftLayer.position = centerLayer.position - Vector3.right * spriteWidth;
        rightLayer.position = centerLayer.position + Vector3.right * spriteWidth;

        lastCamX = cam.position.x;
    }

    void LateUpdate()
    {
        if (cam == null || centerLayer == null) return;

        // 카메라 이동량 계산
        float camDeltaX = cam.position.x - lastCamX;

        // 각 레이어 이동
        centerLayer.position += Vector3.right * camDeltaX * parallaxEffect;
        leftLayer.position += Vector3.right * camDeltaX * parallaxEffect;
        rightLayer.position += Vector3.right * camDeltaX * parallaxEffect;

        lastCamX = cam.position.x;

        // 무한 반복 처리
        if (cam.position.x - centerLayer.position.x > spriteWidth / 2f)
            ShiftRight();
        else if (cam.position.x - centerLayer.position.x < -spriteWidth / 2f)
            ShiftLeft();
    }

    private void ShiftRight()
    {
        // 왼쪽 레이어를 오른쪽 끝으로 이동
        Transform temp = leftLayer;
        leftLayer = centerLayer;
        centerLayer = rightLayer;
        rightLayer = temp;

        rightLayer.position = centerLayer.position + Vector3.right * spriteWidth;
    }

    private void ShiftLeft()
    {
        // 오른쪽 레이어를 왼쪽 끝으로 이동
        Transform temp = rightLayer;
        rightLayer = centerLayer;
        centerLayer = leftLayer;
        leftLayer = temp;

        leftLayer.position = centerLayer.position - Vector3.right * spriteWidth;
    }
}


