using System;
using System.Collections;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;
using HarmonyLib;

namespace CoronaMod;

[HarmonyPatch(typeof(ArtilleryShellItem))]

public class ArtilleryShellItem : PhysicsProp, IHittable, IShockableWithGun
{
    [Header("Artillery Shell Settings")]
    public float explodeHeight;

	private float fallHeight;

    public float killRange;

    public float damageRange;

    public float pushRange;

    public int nonLethalDamage;

    public float physicsForce;

	public float delayedDetonationTime;

	private bool hasExploded;

	private Vector3 startPosition;

	[Space(5f)]
	public GameObject explosionPrefab;

	[Space(5f)]
    public bool explodeOnHit;

    public float explodeOnHitChance;

	public bool explodeOnDrop;

	public float explodeOnDropChance;

    public bool explodeOnBlast;

    public bool explodeOnMauling;

    public bool explodeOnCrushing;

    public bool explodeOnFalling;

	public bool explodeOnGunshot;

	public bool explodeOnLightning;

	public bool explodeOnShockWithGun;

    [Space(5f)]
    public AudioSource shellHitSource;

	public AudioSource shellDetonateSource;

    public AudioClip shellHit;

	public AudioClip shellDud;

    public AudioClip shellDetonate;

    public AudioClip shellDetonateFar;

	public AudioClip shellArmed;

	public override void Start()
	{
		base.Start();

		float f1 = 55f*0.4f;
		float t1 = 340f*0.4f;
		float f2 = 0.5f;
		float t2 = 1.5f;
		float m = (base.scrapValue - f1) / (t1 - f1) * (t2 - f2) + f2;
		Debug.Log($"Shell price: {base.scrapValue}");
		Debug.Log($"Shell danger: {m}");
		killRange = killRange*m;
		damageRange = damageRange*m;
		pushRange = pushRange*m;
		hasExploded = false;
	}

	// private void OnTriggerEnter(Collider other)
	// {
	// 	if (other.gameObject.name == "ItemDropRegion")
	// 	{
	// 		base.isInElevator == true;
	// 	}
	// }

	// private void OnTriggerExit(Collider other)
	// {
	// 	if (other.gameObject.name == "ItemDropRegion")
	// 	{
	// 		base.isInElevator == false;
	// 	}
	// }

	public override void DiscardItem()
	{
		startPosition = base.transform.position;
		if (base.transform.parent != null)
		{
			startPosition = base.transform.parent.InverseTransformPoint(startPosition);
		}

		if (playerHeldBy.isPlayerDead == true)
		{

			//DIED BY BLAST
			if (explodeOnBlast && playerHeldBy.causeOfDeath == CauseOfDeath.Blast)
			{
				Detonate();
			}

			//DIED BY MAULING
			else if (explodeOnMauling && playerHeldBy.causeOfDeath == CauseOfDeath.Mauling)
			{
				Detonate();
			}

			//DIED BY CRUSHING
			else if (explodeOnCrushing && playerHeldBy.causeOfDeath == CauseOfDeath.Crushing)
			{
				Detonate();
			}

			//DIED BY FALLING
			else if (explodeOnFalling && playerHeldBy.causeOfDeath == CauseOfDeath.Gravity)
			{
				Detonate();
			}

			//DIED BY GUNSHOT
			else if (explodeOnGunshot && playerHeldBy.causeOfDeath == CauseOfDeath.Gunshots)
			{
				Detonate();
			}
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
		Debug.Log($"Fall height: {fallHeight}");

		float c = UnityEngine.Random.Range(0,100);

		if (fallHeight > explodeHeight && explodeOnDrop == true && !base.isInShipRoom && !base.isInElevator)
		{
			if (c < explodeOnDropChance)
			{
				Detonate();
			}
			else
			{
				shellDetonateSource.pitch = UnityEngine.Random.Range(0.75f, 1.07f);
				shellDetonateSource.PlayOneShot(shellDud, 1f);
			}
		}
	}

    public void Detonate()
    {
        Debug.Log("Artillery shell detonated!");
        shellDetonateSource.PlayOneShot(shellDetonate, 1f);
        Explode(base.transform.position + Vector3.up, killRange, damageRange, pushRange, nonLethalDamage, physicsForce);
    }

	private IEnumerator DelayDetonate(float delay)
	{
		yield return new WaitForSeconds(delay);
		Detonate();
	}

    public void Explode(Vector3 explosionPosition, float killRange, float damageRange, float pushRange, int nonLethalDamage, float physicsForce, GameObject overridePrefab = null, bool goThroughCar = false)
    {

		GameObject gameObject = UnityEngine.Object.Instantiate(explosionPrefab, explosionPosition, Quaternion.identity, RoundManager.Instance.mapPropsContainer.transform);
		gameObject.SetActive(value: true);

        //DETERMINE DISTANCE FROM EXPLOSION
        float num = Vector3.Distance(GameNetworkManager.Instance.localPlayerController.transform.position, explosionPosition);

        //SHAKE SCREEN DEPENDING ON DISTANCE FROM EXPLOSION
        if (num < 14f)
		{
			HUDManager.Instance.ShakeCamera(ScreenShakeType.VeryStrong);
		}
		else if (num < 25f)
		{
			HUDManager.Instance.ShakeCamera(ScreenShakeType.Big);
		}

        //INSTANCE VARIABLES
        bool flag = false;
		Collider[] array = Physics.OverlapSphere(explosionPosition, pushRange, 2621448, QueryTriggerInteraction.Collide);
		PlayerControllerB playerControllerB = null;
		RaycastHit hitInfo;

        //RUN FOR EVERYTHING IN THE RADIUS OF THE EXPLOSION
        for (int i = 0; i < array.Length; i++)
		{

            //CHECK DISTANCE OF CURRENT ARRAY ITEM FROM EXPLOSION
			float num2 = Vector3.Distance(explosionPosition, array[i].transform.position);

			if (Physics.Linecast(explosionPosition, array[i].transform.position + Vector3.up * 0.3f, out hitInfo, 1073742080, QueryTriggerInteraction.Ignore) && ((!goThroughCar && hitInfo.collider.gameObject.layer == 30) || num2 > 4f))
			{
				continue;
			}

            //FOR PLAYERS
			if (array[i].gameObject.layer == 3 && !flag)
			{
				playerControllerB = array[i].gameObject.GetComponent<PlayerControllerB>();
				if (playerControllerB != null && playerControllerB.IsOwner)
				{
					flag = true;
					if (num2 < killRange)
					{
						Vector3 bodyVelocity = Vector3.Normalize(playerControllerB.gameplayCamera.transform.position - explosionPosition) * 80f / Vector3.Distance(playerControllerB.gameplayCamera.transform.position, explosionPosition);
						playerControllerB.KillPlayer(bodyVelocity, spawnBody: true, CauseOfDeath.Blast);
					}
					else if (num2 < damageRange)
					{
						Vector3 bodyVelocity = Vector3.Normalize(playerControllerB.gameplayCamera.transform.position - explosionPosition) * 80f / Vector3.Distance(playerControllerB.gameplayCamera.transform.position, explosionPosition);
						playerControllerB.DamagePlayer(nonLethalDamage, hasDamageSFX: true, callRPC: true, CauseOfDeath.Blast, 0, fallDamage: false, bodyVelocity);
					}
				}
			}

            //FOR ITEMS
            else if (array[i].gameObject.layer == 6)
            {
                ArtilleryShellItem componentInChildren = array[i].gameObject.GetComponentInChildren<ArtilleryShellItem>();
                if (componentInChildren != null && num2 < killRange)
                {
					Debug.Log("Additional shell within kill range.");
                    componentInChildren.Detonate();
                }
            }

            //FOR LANDMINES
			else if (array[i].gameObject.layer == 21)
			{
				Landmine componentInChildren = array[i].gameObject.GetComponentInChildren<Landmine>();
				if (componentInChildren != null && !componentInChildren.hasExploded && num2 < 6f)
				{
					componentInChildren.Detonate();
				}
			}

            //FOR ENEMIES
			else if (array[i].gameObject.layer == 19)
			{
				EnemyAICollisionDetect componentInChildren2 = array[i].gameObject.GetComponentInChildren<EnemyAICollisionDetect>();
				if (componentInChildren2 != null && componentInChildren2.mainScript.IsOwner && num2 < 4.5f)
				{
					componentInChildren2.mainScript.HitEnemyOnLocalClient(6);
					componentInChildren2.mainScript.HitFromExplosion(num2);
				}
			}
		}

        playerControllerB = GameNetworkManager.Instance.localPlayerController;

        //PHYSICS FORCE
        if (physicsForce > 0f && Vector3.Distance(playerControllerB.transform.position, explosionPosition) < pushRange && !Physics.Linecast(explosionPosition, playerControllerB.transform.position + Vector3.up * 0.3f, out hitInfo, 256, QueryTriggerInteraction.Ignore))
		{
			float num3 = Vector3.Distance(playerControllerB.transform.position, explosionPosition);
			Vector3 vector = Vector3.Normalize(playerControllerB.transform.position + Vector3.up * num3 - explosionPosition) / (num3 * 0.35f) * physicsForce;
			if (vector.magnitude > 2f)
			{
				if (vector.magnitude > 10f)
				{
					playerControllerB.CancelSpecialTriggerAnimations();
				}
				if (!playerControllerB.inVehicleAnimation || (playerControllerB.externalForceAutoFade + vector).magnitude > 50f)
				{
					playerControllerB.externalForceAutoFade += vector;
				}
			}
		}

        VehicleController vehicleController = UnityEngine.Object.FindObjectOfType<VehicleController>();
		if (vehicleController != null && !vehicleController.magnetedToShip && physicsForce > 0f && Vector3.Distance(vehicleController.transform.position, explosionPosition) < pushRange)
		{
			vehicleController.mainRigidbody.AddExplosionForce(physicsForce * 50f, explosionPosition, 12f, 3f, ForceMode.Impulse);
		}
		int num4 = ~LayerMask.GetMask("Room");
		num4 = ~LayerMask.GetMask("Colliders");
		array = Physics.OverlapSphere(explosionPosition, 10f, num4);
		for (int j = 0; j < array.Length; j++)
		{
			Rigidbody component = array[j].GetComponent<Rigidbody>();
			if (component != null)
			{
				component.AddExplosionForce(70f, explosionPosition, 10f);
			}
		}

		hasExploded = true;

		GameObject thisObject = this.gameObject;
		UnityEngine.Object.Destroy(thisObject);

    }

    //HITTABLE PARAMS
    bool IHittable.Hit(int force, Vector3 hitDirection, PlayerControllerB playerWhoHit = null, bool playHitSFX = true, int hitID = -1)
	{

        Debug.Log("Shell hit.");
        shellHitSource.pitch = UnityEngine.Random.Range(0.75f, 1.07f);
        shellHitSource.PlayOneShot(shellHit, 1f);

        if (explodeOnHit == true)
        {
			shellDetonateSource.pitch = UnityEngine.Random.Range(0.75f, 1.07f);
            float c = UnityEngine.Random.Range(0,100);
            if (c < explodeOnHitChance)
			{
				Debug.Log("Shell armed.");
				shellDetonateSource.PlayOneShot(shellArmed, 1f);

				StartCoroutine(DelayDetonate(delayedDetonationTime));
			}
			else
			{
				Debug.Log("Shell not armed.");
				//shellDetonateSource.PlayOneShot(shellDud, 1f);
			}
        }

        return true;
	}

	//SHOCKABLE PARAMS
	bool IShockableWithGun.CanBeShocked()
	{
		return explodeOnShockWithGun;
	}
	float IShockableWithGun.GetDifficultyMultiplier()
	{
		return 1;
	}
	NetworkObject IShockableWithGun.GetNetworkObject()
	{
		return base.NetworkObject;
	}
	Vector3 IShockableWithGun.GetShockablePosition()
	{
		return base.transform.position;
	}
	Transform IShockableWithGun.GetShockableTransform()
	{
		return base.transform;
	}
	void IShockableWithGun.ShockWithGun(PlayerControllerB shockedByPlayer)
	{
		if (explodeOnShockWithGun == true)
		{
			Detonate();
		}
	}
	void IShockableWithGun.StopShockingWithGun()
	{
	}

}