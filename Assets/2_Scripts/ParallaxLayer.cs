using UnityEngine;

public class ParallaxLayer : MonoBehaviour
{
    [Header("Camera & Parallax")]
    [SerializeField] private Transform cam;
    [SerializeField, Range(0f, 1f)] private float parallaxEffect = 0.3f;

    [Header("Background Layers")]
    [SerializeField] private Transform centerLayer; // Inspector���� �߾� ���̾� ����

    private Transform leftLayer;
    private Transform rightLayer;
    private float lastCamX;
    private float spriteWidth;

    void Start()
    {
        // ī�޶� �ڵ� �Ҵ�
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

        // �¿� ���̾� ����
        leftLayer = Instantiate(centerLayer, centerLayer.position, Quaternion.identity, transform);
        rightLayer = Instantiate(centerLayer, centerLayer.position, Quaternion.identity, transform);

        // ���� ���� ���� �� ��� (PPU, ������ �ݿ�)
        SpriteRenderer sr = centerLayer.GetComponent<SpriteRenderer>();
        spriteWidth = sr.bounds.size.x;

        // �¿� ���̾� ��ġ �ʱ�ȭ
        leftLayer.position = centerLayer.position - Vector3.right * spriteWidth;
        rightLayer.position = centerLayer.position + Vector3.right * spriteWidth;

        lastCamX = cam.position.x;
    }

    void LateUpdate()
    {
        if (cam == null || centerLayer == null) return;

        // ī�޶� �̵��� ���
        float camDeltaX = cam.position.x - lastCamX;

        // �� ���̾� �̵�
        centerLayer.position += Vector3.right * camDeltaX * parallaxEffect;
        leftLayer.position += Vector3.right * camDeltaX * parallaxEffect;
        rightLayer.position += Vector3.right * camDeltaX * parallaxEffect;

        lastCamX = cam.position.x;

        // ���� �ݺ� ó��
        if (cam.position.x - centerLayer.position.x > spriteWidth / 2f)
            ShiftRight();
        else if (cam.position.x - centerLayer.position.x < -spriteWidth / 2f)
            ShiftLeft();
    }

    private void ShiftRight()
    {
        // ���� ���̾ ������ ������ �̵�
        Transform temp = leftLayer;
        leftLayer = centerLayer;
        centerLayer = rightLayer;
        rightLayer = temp;

        rightLayer.position = centerLayer.position + Vector3.right * spriteWidth;
    }

    private void ShiftLeft()
    {
        // ������ ���̾ ���� ������ �̵�
        Transform temp = rightLayer;
        rightLayer = centerLayer;
        centerLayer = leftLayer;
        leftLayer = temp;

        leftLayer.position = centerLayer.position - Vector3.right * spriteWidth;
    }
}


