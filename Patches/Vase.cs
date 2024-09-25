using System;
using System.Collections;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;
using HarmonyLib;

namespace CoronaMod;

[HarmonyPatch(typeof(ArtilleryShellItem))]

public class Vase : PhysicsProp, IHittable
{
    [Header("Vase Settings")]
    public float breakHeight;

    private float fallHeight;

    private bool broken;

    private Vector3 startPosition;

    public float minWobbleTime;

    public float maxWobbleTime;

    public float safePlaceTime;

    private bool safePlaced;

    [Space(5f)]
    public bool breakOnHit;

    public bool breakOnDrop;

    public bool breakOnDeath;

    public bool breakOnBump;

    public bool breakOnEnemy;

    public bool breakOnBlast;

    public bool breakInShip;

    [Space(5f)]
    public AudioSource vaseAudio;

    public AudioClip vaseWobble;

    public AudioClip vaseBreak;

    public override void Start()
    {
        base.Start();
    }

    public override void DiscardItem()
    {
        safePlaced = true;
        startPosition = base.transform.position;
		if (base.transform.parent != null)
		{
			startPosition = base.transform.parent.InverseTransformPoint(startPosition);
		}

        if (playerHeldBy.isPlayerDead && breakOnDeath)
        {
            Shatter();
        }

        base.DiscardItem();
    }

    public override void OnHitGround()
    {
        base.OnHitGround();

        Vector3 fallPosition = base.transform.position;
		if (base.transform.parent != null)
		{
			fallPosition = base.transform.parent.InverseTransformPoint(fallPosition);
		}

		fallHeight = Vector3.Distance(fallPosition,startPosition);
		Debug.Log($"Vase fell: {fallHeight}");

        placeSafely(safePlaceTime);

        if (fallHeight > breakHeight && breakOnDrop)
        {
            Shatter();
        }
    }

    public void Shatter()
    {
        Debug.Log("Vase shattered!");
        vaseAudio.pitch = UnityEngine.Random.Range(0.75f, 1.07f);
        vaseAudio.PlayOneShot(vaseBreak);
        broken = true;
        GameObject thisObject = this.gameObject;
		UnityEngine.Object.Destroy(thisObject);
    }

    public IEnumerator Wobble(float time)
    {
        Debug.Log("Vase wobbling...");
        vaseAudio.pitch = UnityEngine.Random.Range(0.75f, 1.07f);
        vaseAudio.PlayOneShot(vaseWobble);
        yield return new WaitForSeconds(time);
        Shatter();
    }

    public IEnumerator placeSafely(float time)
    {
        yield return new WaitForSeconds(safePlaceTime);
        safePlaced = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (base.isInShipRoom && !breakInShip || safePlaced == true)
        {
            return;
        }

        //PLAYER COLLISION
        else if (other.gameObject.layer == 3 && breakOnBump)
        {
            Debug.Log("Vase bumped by player.");
            StartCoroutine(Wobble(UnityEngine.Random.Range(minWobbleTime,maxWobbleTime)));
        }

        //ENEMY COLLISION
        else if (other.gameObject.layer == 19 && breakOnEnemy)
        {
            Debug.Log("Vase bumped by enemy.");
            StartCoroutine(Wobble(UnityEngine.Random.Range(minWobbleTime,maxWobbleTime)));
        }

        //BLAST COLLISION
        // else if (other.gameObject.layer == )
    }

    bool IHittable.Hit(int force, Vector3 hitDirection, PlayerControllerB playerWhoHit = null, bool playHitSFX = true, int hitID = -1)
	{
        if (breakOnHit)
        {
            Shatter();
        }
        return true;
	}

}