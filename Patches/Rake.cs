using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;

public class Rake : PhysicsProp
{

    [Header("Rake Settings")]
    public int damageDealtPlayer;

    public int damageDealtEnemy;

    public float physicsForce;

    private Animator animator;

    private bool rakeFlipped;

    private GameObject activator;

    [Space(5f)]
    public AudioSource rakeAudio;

    public AudioClip rakeFlip;

    public AudioClip rakeFall;

    public AudioClip reelUp;

    public AudioClip rakeSwing;

    public AudioClip[] hitSFX;

    [Space(5f)]
    [Header("Weapon Variables")]
    public int rakeHitForce = 1;

    public bool reelingUp;

    public bool isHoldingButton;

    private RaycastHit rayHit;

    private Coroutine reelingUpCoroutine;

    private RaycastHit[] objectsHitByRake;

    private List<RaycastHit> objectsHitByRakeList = new List<RaycastHit>();

    private PlayerControllerB previousPlayerHeldBy;

    private int rakeMask = 1084754248;

    private void Start()
    {
        animator = base.gameObject.GetComponent<Animator>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isHeld || rakeFlipped)
        {
            return;
        }

        //PLAYER COLLISION
        else if (other.gameObject.layer == 3)
        {
            Debug.Log("Rake activated by player.");
            activator = other.gameObject;
            PlayerControllerB player = other.gameObject.GetComponent<PlayerControllerB>();
            Vector3 bodyVelocity = Vector3.Normalize(player.gameplayCamera.transform.position - base.transform.position) * 80f / Vector3.Distance(player.gameplayCamera.transform.position, base.transform.position);
            Flip();
            player.DamagePlayer(damageDealtPlayer, hasDamageSFX: true, callRPC: true, CauseOfDeath.Bludgeoning, 0, fallDamage:false, bodyVelocity);

            //PLAYER PHYSICS
            if (physicsForce > 0f)
            {
                Vector3 vector = Vector3.Normalize(player.transform.position - base.transform.position);
                if (vector.magnitude > 10f)
                {
                    player.CancelSpecialTriggerAnimations();
                }
                if (!player.inVehicleAnimation || (player.externalForceAutoFade + vector).magnitude > 50f)
                {
                    player.externalForceAutoFade += vector;
                }
            }
        }

        //ENEMY COLLISION
        else if (other.gameObject.layer == 19)
        {
            Debug.Log("Rake activated by enemy.");
            activator = other.gameObject;
            EnemyAICollisionDetect enemy = other.gameObject.GetComponent<EnemyAICollisionDetect>();
            enemy.mainScript.HitEnemyOnLocalClient(damageDealtEnemy);
            Flip();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject == activator) {
            Fall();
        }
    }

    private void Flip()
    {
        rakeFlipped = true;
        animator.Play("flip");
        rakeAudio.pitch = UnityEngine.Random.Range(0.75f, 1.07f);
        rakeAudio.PlayOneShot(rakeFlip);
    }

    private void Fall()
    {
        rakeFlipped = false;
        animator.Play("fall");
        rakeAudio.pitch = UnityEngine.Random.Range(0.75f, 1.07f);
        rakeAudio.PlayOneShot(rakeFall);
    }

    public override void ItemActivate(bool used, bool buttonDown = true)
    {
        if (playerHeldBy == null)
        {
            return;
        }
        isHoldingButton = buttonDown;
        if (!reelingUp && buttonDown)
        {
            reelingUp = true;
            previousPlayerHeldBy = playerHeldBy;
            if (reelingUpCoroutine != null)
            {
                StopCoroutine(reelingUpCoroutine);
            }
            reelingUpCoroutine = StartCoroutine(reelUpRake());
        }
    }

    private IEnumerator reelUpRake()
    {
        playerHeldBy.activatingItem = true;
        playerHeldBy.twoHanded = true;
        playerHeldBy.playerBodyAnimator.ResetTrigger("shovelHit");
        playerHeldBy.playerBodyAnimator.SetBool("reelingUp", value: true);
        rakeAudio.PlayOneShot(reelUp);
        ReelUpSFXServerRpc();
        yield return new WaitForSeconds(0.35f);
        yield return new WaitUntil(() => !isHoldingButton || !isHeld);
        SwingRake(!isHeld);
        yield return new WaitForSeconds(0.13f);
        yield return new WaitForEndOfFrame();
        HitRake(!isHeld);
        yield return new WaitForSeconds(0.3f);
        reelingUp = false;
        reelingUpCoroutine = null;
    }

    [ServerRpc]
    public void ReelUpSFXServerRpc()
    {
        {
            ReelUpSFXClientRpc();
        }
    }

    [ClientRpc]
    public void ReelUpSFXClientRpc()
    {
        if(!base.IsOwner)
        {
            rakeAudio.pitch = 1;
            rakeAudio.PlayOneShot(reelUp);
        }
    }

    public override void DiscardItem()
    {
        if (playerHeldBy != null)
        {
            playerHeldBy.activatingItem = false;
        }
        base.DiscardItem();
    }

    public void SwingRake(bool cancel = false)
    {
        previousPlayerHeldBy.playerBodyAnimator.SetBool("reelingUp", value: false);
        if (!cancel)
        {
            rakeAudio.pitch = 1;
            rakeAudio.PlayOneShot(rakeSwing);
        }
    }

    public void HitRake(bool cancel = false)
    {
        if (previousPlayerHeldBy == null)
        {
            Debug.LogError("Previousplayerheldby is null on this client when HitRake is called.");
            return;
        }
        previousPlayerHeldBy.activatingItem = false;
        bool flag = false;
        bool flag2 = false;
        bool flag3 = false;
        int num = -1;
        if (!cancel)
        {
            previousPlayerHeldBy.twoHanded = false;
            objectsHitByRake = Physics.SphereCastAll(previousPlayerHeldBy.gameplayCamera.transform.position + previousPlayerHeldBy.gameplayCamera.transform.right * -0.35f, 0.8f, previousPlayerHeldBy.gameplayCamera.transform.forward, 1.5f, rakeMask, QueryTriggerInteraction.Collide);
            objectsHitByRakeList = objectsHitByRake.OrderBy((RaycastHit x) => x.distance).ToList();
            List<EnemyAI> list = new List<EnemyAI>();

            for (int i = 0; i < objectsHitByRakeList.Count; i++)
            {
                if (objectsHitByRakeList[i].transform.gameObject.layer == 8 || objectsHitByRakeList[i].transform.gameObject.layer == 11)
                {
                    if (objectsHitByRakeList[i].collider.isTrigger)
                    {
                        continue;
                    }
                    flag = true;
                    string text = objectsHitByRake[i].collider.gameObject.tag;
                    for (int j = 0; j < StartOfRound.Instance.footstepSurfaces.Length; j++)
                    {
                        if (StartOfRound.Instance.footstepSurfaces[j].surfaceTag == text)
                        {
                            num = j;
                            break;
                        }
                    }
                }
                else
                {
                    if (!objectsHitByRakeList[i].transform.TryGetComponent<IHittable>(out var component) || objectsHitByRakeList[i].transform == previousPlayerHeldBy.transform || (!(objectsHitByRakeList[i].point == Vector3.zero) && Physics.Linecast(previousPlayerHeldBy.gameplayCamera.transform.position, objectsHitByRakeList[i].point, out var _, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore)))
                    {
                        continue;
                    }
                    flag = true;
                    Vector3 forward = previousPlayerHeldBy.gameplayCamera.transform.forward;
                    try
                    {
                        EnemyAICollisionDetect component2 = objectsHitByRakeList[i].transform.GetComponent<EnemyAICollisionDetect>();
                        if (component2 != null)
                        {
                            if (!(component2.mainScript == null) && !list.Contains(component2.mainScript))
                            {
                                goto IL_02ff;
                            }
                            continue;
                        }
                        if (!(objectsHitByRakeList[i].transform.GetComponent<PlayerControllerB>() != null))
                        {
                            goto IL_02ff;
                        }
                        if (!flag3)
                        {
                            flag3 = true;
                            goto IL_02ff;
                        }
                        goto end_IL_0288;
                        IL_02ff:
                        bool flag4 = component.Hit(rakeHitForce, forward, previousPlayerHeldBy, playHitSFX: true, 1);
                        if (flag4 && component2 != null)
                        {
                            list.Add(component2.mainScript);
                        }
                        if (!flag2)
                        {
                            flag2 = flag4;
                        }
                        end_IL_0288:;
                    }
                    catch (Exception arg)
                    {
                        Debug.Log($"Exception caught when hitting object with rake from player # {previousPlayerHeldBy.playerClientId}: {arg}");
                    }
                }
            }
        }
        if (flag)
        {
            RoundManager.PlayRandomClip(rakeAudio, hitSFX);
            UnityEngine.Object.FindObjectOfType<RoundManager>().PlayAudibleNoise(base.transform.position, 17f, 0.8f);
            if (!flag2 && num != -1)
            {
                rakeAudio.PlayOneShot(StartOfRound.Instance.footstepSurfaces[num].hitSurfaceSFX);
                WalkieTalkie.TransmitOneShotAudio(rakeAudio, StartOfRound.Instance.footstepSurfaces[num].hitSurfaceSFX);
            }
            playerHeldBy.playerBodyAnimator.SetTrigger("shovelHit");
            HitRakeServerRpc(num);
        }
    }

    [ServerRpc]
    public void HitRakeServerRpc(int hitSurfaceID)
    {
        {
            HitRakeClientRpc(hitSurfaceID);
        }
    }

    [ClientRpc]
    public void HitRakeClientRpc(int hitSurfaceID)
    {
        if(!base.IsOwner)
        {
            RoundManager.PlayRandomClip(rakeAudio, hitSFX);
            if (hitSurfaceID != -1)
            {
                HitSurfaceWithRake(hitSurfaceID);
            }
        }
    }

    private void HitSurfaceWithRake(int hitSurfaceID)
    {
        rakeAudio.PlayOneShot(StartOfRound.Instance.footstepSurfaces[hitSurfaceID].hitSurfaceSFX);
        WalkieTalkie.TransmitOneShotAudio(rakeAudio, StartOfRound.Instance.footstepSurfaces[hitSurfaceID].hitSurfaceSFX);
    }
}