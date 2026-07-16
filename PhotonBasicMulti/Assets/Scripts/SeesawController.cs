using UnityEngine;

public class SeesawController : MonoBehaviour
{
    [Header("조작 설정")]
    [Tooltip("스마트폰 기울기에 따른 회전 가속 속도")]
    public float rotationSpeed = 50f;

    [Tooltip("시소 판이 기울어질 수 있는 최대 각도")]
    public float maxAngle = 45f;

    // 현재 판의 목표 Z 회전값 (계속 누적됨)
    private float currentZRotation = 0f;

    void Update()
    {
        // 1. 가속도 센서 값 가져오기
        float tiltX = Input.acceleration.x;

#if UNITY_EDITOR
        // 에디터 테스트용 키보드 입력
        if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) tiltX = -1f;
        else if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) tiltX = 1f;
#endif

        // 2. 중요: 폰을 똑바로 세워도(tiltX가 0이 되어도) 기존 각도에서 복귀하지 않고, 
        // 폰을 기울이고 있는 "시간" 동안 회전 각도가 계속 "누적"되어 추가됩니다.
        // (왼쪽으로 기울이면 계속 왼쪽으로 쓰러짐)
        if (Mathf.Abs(tiltX) > 0.05f) // 미세한 흔들림 무시(데드존)
        {
            // 3D 씬 구성에 따라 방향이 반대라면 rotationSpeed 앞의 부호(+/-)를 바꿔주세요.
            currentZRotation -= tiltX * rotationSpeed * Time.deltaTime;
        }

        // 3. 무한정 돌아가지 않게 최대 각도 제한
        currentZRotation = Mathf.Clamp(currentZRotation, -maxAngle, maxAngle);

        // 4. 계산된 각도를 시소판에 부드럽게 적용
        // (현재 카메라 뷰 기준으로 Z축 회전인지 X축 회전인지 확인 후 적용)
        Quaternion targetRotation = Quaternion.Euler(0, 0, currentZRotation);
        transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * 15f);
    }
}
