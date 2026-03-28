using UnityEngine;
using Photon.Pun;
using UnityEngine.SceneManagement;
using TMPro; // UI 글씨 필수
using System.Collections; // [추가★] 코루틴(시간 지연)을 쓰기 위한 필수 단어!

public class PlayerMove : MonoBehaviourPun
{
    // [수정 POINT★] 걷기와 뛰기 속도를 따로 나눴습니다! (입맛대로 조절하십쇼)
    public float walkSpeed = 3f;
    public float runSpeed = 7f;

    private Animator anim;
    private Unity.Cinemachine.CinemachineCamera vcam;
    private Unity.Cinemachine.CinemachineOrbitalFollow orbitalRig;

    public string myRole = "";

    void Start()
    {
        anim = GetComponent<Animator>();

        if (photonView.IsMine)
        {
            if (PhotonNetwork.LocalPlayer.CustomProperties.ContainsKey("Role"))
            {
                myRole = (string)PhotonNetwork.LocalPlayer.CustomProperties["Role"];

                StartCoroutine(ShowRoleSequence());

                // 콘솔창에 빨간색/파란색으로 직업 띄워주기!
                if (myRole == "Seeker")
                {
                    Debug.Log("<color=red>You Are Seeker!</color>");
                }
                else
                {
                    Debug.Log("<color=blue>You Are Surviver!</color>");
                }
            }



            vcam = FindFirstObjectByType<Unity.Cinemachine.CinemachineCamera>();

            if (vcam != null)
            {
                vcam.Follow = this.transform;
                vcam.LookAt = this.transform;
                orbitalRig = vcam.GetComponent<Unity.Cinemachine.CinemachineOrbitalFollow>();

                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
    }

    IEnumerator ShowRoleSequence()
    {
        // 1. 씬에서 두 개의 텍스트를 찾습니다.
        GameObject centerObj = GameObject.Find("CenterRoleText");
        GameObject cornerObj = GameObject.Find("CornerRoleText");

        // [방어막 1] 이름을 못 찾으면 에러 내지 말고 친절하게 알려주기!
        if (centerObj == null || cornerObj == null)
        {
            Debug.LogError("🚨 [긴급] 형님! UI 텍스트를 못 찾았습니다! 하이어라키 창에서 이름을 정확히 CenterRoleText, CornerRoleText로 바꿨는지(대소문자), 활성화되어 있는지 확인해 주십쇼!");
            yield break; // 여기서 안전하게 함수 종료 (캐릭터 멈춤 방지!)
        }

        TextMeshProUGUI centerText = centerObj.GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI cornerText = cornerObj.GetComponent<TextMeshProUGUI>();

        // [방어막 2] TextMeshPro가 아니면 알려주기!
        if (centerText == null || cornerText == null)
        {
            Debug.LogError("🚨 [긴급] 형님! TextMeshProUGUI 컴포넌트가 없습니다! 혹시 구버전 일반 Text를 만드신 건 아닌지 확인해 주십쇼!");
            yield break; // 안전하게 종료
        }

        // 여기까지 무사히 통과했다면? 정상적으로 컷씬 연출 시작!
        centerObj.SetActive(true);
        cornerObj.SetActive(false);

        if (myRole == "Seeker")
        {
            centerText.text = "<color=red>You Are Seeker!</color>";
            cornerText.text = "<color=red>Seeker</color>";
        }
        else
        {
            centerText.text = "<color=#00BFFF>You Are Surviver!</color>";
            cornerText.text = "<color=#00BFFF>Surviver</color>";
        }

        // 3초 대기
        yield return new WaitForSeconds(3f);

        // 3초 뒤 구석으로 이동!
        centerObj.SetActive(false);
        cornerObj.SetActive(true);
    }

    void Update()
        {
            if (!photonView.IsMine) return;

            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            Vector3 moveDir = Vector3.zero;

            bool isAltLooking = Input.GetKey(KeyCode.LeftAlt);

            // [추가 POINT★] 왼쪽 Shift 키를 누르고 있는지 감지!
            bool isRunning = Input.GetKey(KeyCode.LeftShift);

            // Alt 뗐을 때 카메라 복귀
            if (Input.GetKeyUp(KeyCode.LeftAlt) && orbitalRig != null)
            {
                orbitalRig.HorizontalAxis.Value = transform.eulerAngles.y;
            }

            // 키보드 입력이 있을 때 (이동 중일 때)
            if (h != 0 || v != 0)
            {
                if (Camera.main != null && SceneManager.GetActiveScene().name != "LobbyScene")
                {
                    if (isAltLooking)
                    {
                        moveDir = (transform.forward * v + transform.right * h).normalized;
                    }
                    else
                    {
                        Vector3 camForward = Camera.main.transform.forward;
                        camForward.y = 0;
                        Vector3 camRight = Camera.main.transform.right;
                        camRight.y = 0;
                        moveDir = (camForward.normalized * v + camRight.normalized * h).normalized;
                    }
                }
                else
                {
                    moveDir = new Vector3(h, 0, v).normalized;
                }

                // Alt 안 누를 때만 몸통 회전
                if (!isAltLooking)
                {
                    transform.forward = Vector3.Slerp(transform.forward, moveDir, Time.deltaTime * 10f);
                }

                // [핵심★] Shift 누르면 runSpeed(7), 안 누르면 walkSpeed(3)로 이동!
                float currentSpeed = isRunning ? runSpeed : walkSpeed;
                transform.Translate(moveDir * currentSpeed * Time.deltaTime, Space.World);

                // [핵심★] 애니메이터에 보내는 신호도 걷기(0.5)와 뛰기(1.0)로 나눠서 보냅니다!
                float animValue = isRunning ? 1.0f : 0.5f;
                if (anim != null) anim.SetFloat("MoveSpeed", animValue);
            }
            else
            {
                // 가만히 있을 때 (정지)
                if (anim != null) anim.SetFloat("MoveSpeed", 0f);
            }
        }
    }

