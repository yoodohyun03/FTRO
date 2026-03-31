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
        // 가운데 텍스트는 이제 GameManager가 하니까 안 찾습니다!
        GameObject cornerObj = GameObject.Find("CornerRoleText");
        GameObject blindObj = GameObject.Find("BlindPanel");

        if (cornerObj == null)
        {
            Debug.LogError("🚨 UI 텍스트(CornerRoleText)를 못 찾았습니다!");
            yield break;
        }

        TextMeshProUGUI cornerText = cornerObj.GetComponent<TextMeshProUGUI>();

        // 구석 텍스트는 처음부터 바로 켜줍니다.
        cornerObj.SetActive(true);

        // 직업에 따른 '구석 텍스트'와 '안대' 세팅
        if (myRole == "Seeker")
        {
            cornerText.text = "<color=red>Seeker</color>";
            if (blindObj != null) blindObj.SetActive(true); // 술래는 눈 감아!
        }
        else
        {
            cornerText.text = "<color=#00BFFF>Surviver</color>";
            if (blindObj != null) blindObj.SetActive(false); // 생존자는 눈 떠!
        }

        // 5초 대기 (GameManager의 준비 시간 5초와 완벽히 맞춤!)
        yield return new WaitForSeconds(5f);

        // 5초 뒤: 술래 안대 벗겨주기! (추격 시작)
        if (blindObj != null) blindObj.SetActive(false);
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

