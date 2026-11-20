using System.Collections;
using UnityEngine;

// The hospital's front door. Locked until the kid has the key.
public class ExitDoor : MonoBehaviour
{
    [Tooltip("Hinge transforms the door panels hang from.")]
    [SerializeField] private Transform leftHinge;
    [SerializeField] private Transform rightHinge;
    [SerializeField] private GameObject lockVisual;
    [SerializeField] private float interactDistance = 2.0f;
    [SerializeField] private float openAngle = 100f;
    [SerializeField] private float openTime = 1.8f;

    private bool opened;
    private float lockedCooldown;

    private void Update()
    {
        if (opened) return;
        EscapeGameManager gm = EscapeGameManager.Instance;
        if (gm == null || gm.Player == null || Time.timeScale == 0f) return;

        Vector3 toPlayer = gm.Player.position - transform.position;
        toPlayer.y = 0f;
        if (toPlayer.magnitude > interactDistance) return;

        lockedCooldown -= Time.deltaTime;
        if (!gm.HasKey)
        {
            gm.ShowPrompt("The front door is locked  [E]");
            if (Input.GetKeyDown(KeyCode.E) && lockedCooldown <= 0f)
            {
                lockedCooldown = 1.5f;
                gm.OnDoorLocked();
                StartCoroutine(Rattle());
            }
        }
        else
        {
            gm.ShowPrompt("Press E to unlock the front door");
            if (Input.GetKeyDown(KeyCode.E))
            {
                opened = true;
                if (lockVisual != null) lockVisual.SetActive(false);
                gm.OnExitOpened();
                StartCoroutine(Open());
            }
        }
    }

    private IEnumerator Rattle()
    {
        for (float t = 0f; t < 0.4f; t += Time.deltaTime)
        {
            float a = Mathf.Sin(t * 60f) * 1.5f;
            if (leftHinge) leftHinge.localRotation = Quaternion.Euler(0f, a, 0f);
            if (rightHinge) rightHinge.localRotation = Quaternion.Euler(0f, -a, 0f);
            yield return null;
        }
        if (leftHinge) leftHinge.localRotation = Quaternion.identity;
        if (rightHinge) rightHinge.localRotation = Quaternion.identity;
    }

    private IEnumerator Open()
    {
        for (float t = 0f; t < 1f; t += Time.deltaTime / openTime)
        {
            float e = Mathf.SmoothStep(0f, 1f, t);
            // panels swing outward (away from the lobby)
            if (leftHinge) leftHinge.localRotation = Quaternion.Euler(0f, openAngle * e, 0f);
            if (rightHinge) rightHinge.localRotation = Quaternion.Euler(0f, -openAngle * e, 0f);
            yield return null;
        }
    }
}
