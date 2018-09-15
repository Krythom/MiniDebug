using System;
using System.Collections;
using System.Reflection;
using GlobalEnums;
using UnityEngine;
using UnityEngine.SceneManagement;

// Token: 0x020008FB RID: 2299
public class ModCheats : MonoBehaviour
{
	// Token: 0x06003042 RID: 12354 RVA: 0x000025C3 File Offset: 0x000007C3
	public ModCheats()
	{
	}

	// Token: 0x06003043 RID: 12355
	public void Update()
	{
		if (!HeroController.instance.acceptingInput)
		{
			return;
		}
		if (Input.GetKeyDown(KeyCode.W))
		{
			HeroController.instance.StartCoroutine(HeroController.instance.Die());
		}
		if (Input.GetKeyDown(KeyCode.E))
		{
			this.showSpeed = !this.showSpeed;
		}
		if (Input.GetKeyDown(KeyCode.Q))
		{
			this.noclip = !this.noclip;
			this.noclipPos = HeroController.instance.gameObject.transform.position;
		}
		if (Input.GetKeyDown(KeyCode.F8))
		{
			this.cameraFollow = !this.cameraFollow;
		}
		if (Input.GetKeyDown(KeyCode.PageUp))
		{
			GameCameras.instance.tk2dCam.ZoomFactor *= 1.05f;
		}
		if (Input.GetKeyDown(KeyCode.PageDown))
		{
			GameCameras.instance.tk2dCam.ZoomFactor *= 0.95f;
		}
		if (Input.GetKeyDown(KeyCode.End))
		{
			GameCameras.instance.tk2dCam.ZoomFactor = 1f;
		}
		if (Input.GetKeyDown(KeyCode.Insert))
		{
			HeroController.instance.vignette.enabled = !HeroController.instance.vignette.enabled;
		}
		if (this.noclip)
		{
			if (GameManager.instance.inputHandler.inputActions.left.IsPressed)
			{
				this.noclipPos = new Vector3(this.noclipPos.x - Time.deltaTime * 20f, this.noclipPos.y, this.noclipPos.z);
			}
			if (GameManager.instance.inputHandler.inputActions.right.IsPressed)
			{
				this.noclipPos = new Vector3(this.noclipPos.x + Time.deltaTime * 20f, this.noclipPos.y, this.noclipPos.z);
			}
			if (GameManager.instance.inputHandler.inputActions.up.IsPressed)
			{
				this.noclipPos = new Vector3(this.noclipPos.x, this.noclipPos.y + Time.deltaTime * 20f, this.noclipPos.z);
			}
			if (GameManager.instance.inputHandler.inputActions.down.IsPressed)
			{
				this.noclipPos = new Vector3(this.noclipPos.x, this.noclipPos.y - Time.deltaTime * 20f, this.noclipPos.z);
			}
			if (HeroController.instance.transitionState == HeroTransitionState.WAITING_TO_TRANSITION)
			{
				HeroController.instance.gameObject.transform.position = this.noclipPos;
				return;
			}
			this.noclipPos = HeroController.instance.gameObject.transform.position;
		}
		if (this.cameraFollow)
		{
			ModCheats.cameraGameplayScene.SetValue(GameManager.instance.cameraCtrl, false);
			GameManager.instance.cameraCtrl.camTarget.transform.position = new Vector3(HeroController.instance.transform.position.x, HeroController.instance.transform.position.y, GameManager.instance.cameraCtrl.camTarget.transform.position.z);
			GameManager.instance.cameraCtrl.transform.position = new Vector3(HeroController.instance.transform.position.x, HeroController.instance.transform.position.y, GameManager.instance.cameraCtrl.transform.position.z);
		}
		PlayerData instance = PlayerData.instance;
		HeroController instance2 = HeroController.instance;
		GameManager instance3 = GameManager.instance;
		if (Input.GetKeyDown(KeyCode.O))
		{
			this.SavePos.y = instance2.gameObject.transform.position.y;
			this.SavePos.x = instance2.gameObject.transform.position.x;
			this.SaveScene = instance3.GetSceneNameString();
			this.SaveDeathstate = instance.soulLimited;
			this.SaveShadePos.x = instance.shadePositionX;
			this.SaveShadePos.y = instance.shadePositionY;
			this.SaveShadeScene = instance.shadeScene;
			this.SaveHealth = instance.health;
			this.SaveGeo = instance.geo;
			this.SaveMade = true;
		}
		if (Input.GetKeyDown(KeyCode.L) && this.SaveMade)
		{
			instance3.ChangeToScene(this.SaveScene, "", 0f);
			this.cameraFollow = true;
			instance3.ResetSemiPersistentItems();
			instance.soulLimited = this.SaveDeathstate;
			instance.geo = this.SaveGeo;
			while (instance.health < this.SaveHealth)
			{
				instance2.TakeHealth(1);
			}
			while (instance.health < this.SaveHealth)
			{
				instance2.AddHealth(1);
			}
			if (this.SaveDeathstate)
			{
				instance.shadePositionX = this.SaveShadePos.x;
				instance.shadePositionY = this.SaveShadePos.y;
				instance.shadeScene = this.SaveShadeScene;
			}
		}
	}

	// Token: 0x06003044 RID: 12356 RVA: 0x0002427B File Offset: 0x0002247B
	static ModCheats()
	{
	}

	// Token: 0x06003045 RID: 12357 RVA: 0x00024298 File Offset: 0x00022498
	private IEnumerator DestroyBordersCoroutine()
	{
		yield return new WaitForEndOfFrame();
		try
		{
			if (GameManager.instance.IsGameplayScene())
			{
				HeroController.instance.vignette.enabled = false;
				foreach (GameObject gameObject in UnityEngine.Object.FindObjectsOfType<GameObject>())
				{
					if (gameObject.name.ToLowerInvariant().Contains("sceneborder"))
					{
						UnityEngine.Object.Destroy(gameObject.GetComponent<SpriteRenderer>());
					}
				}
			}
			yield break;
		}
		catch (Exception)
		{
			base.StartCoroutine(this.DestroyBordersCoroutine());
			yield break;
		}
		yield break;
	}

	// Token: 0x06003046 RID: 12358 RVA: 0x000242A7 File Offset: 0x000224A7
	public void OnEnable()
	{
		UnityEngine.SceneManagement.SceneManager.sceneLoaded -= this.DestroyBorders;
		UnityEngine.SceneManagement.SceneManager.sceneLoaded += this.DestroyBorders;
	}

	// Token: 0x06003047 RID: 12359 RVA: 0x000242CB File Offset: 0x000224CB
	public void OnDisable()
	{
		UnityEngine.SceneManagement.SceneManager.sceneLoaded -= this.DestroyBorders;
	}

	// Token: 0x06003048 RID: 12360 RVA: 0x000242DE File Offset: 0x000224DE
	private void DestroyBorders(Scene scene, LoadSceneMode mode)
	{
		base.StartCoroutine(this.DestroyBordersCoroutine());
	}

	// Token: 0x06003049 RID: 12361 RVA: 0x001223F0 File Offset: 0x001205F0
	public void OnGUI()
	{
		bool flag = HeroController.instance.acceptingInput || HeroController.instance.hero_state != ActorStates.no_input;
		if (GameManager.instance.GetSceneNameString() == "Menu_Title" || (flag && this.showSpeed))
		{
			Color backgroundColor = GUI.backgroundColor;
			Color contentColor = GUI.contentColor;
			Color color = GUI.color;
			Matrix4x4 matrix = GUI.matrix;
			GUI.backgroundColor = Color.white;
			GUI.contentColor = Color.white;
			GUI.color = Color.white;
			GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3((float)Screen.width / 1920f, (float)Screen.height / 1080f, 1f));
			if (flag)
			{
				if (this.rb2d == null)
				{
					this.rb2d = HeroController.instance.GetComponent<Rigidbody2D>();
				}
				if (this.collider2d == null)
				{
					this.collider2d = HeroController.instance.GetComponent<BoxCollider2D>();
				}
				GUI.Label(new Rect(0f, 0f, 200f, 200f), string.Format("(X, Y): {0}, {1}", this.rb2d.velocity.x, this.rb2d.velocity.y));
				GUI.Label(new Rect(0f, 50f, 200f, 200f), string.Format("Width x Height: {0} x {1}", this.collider2d.bounds.size.x, this.collider2d.bounds.size.y));
			}
			else
			{
				GUI.Label(new Rect(0f, 0f, 200f, 200f), "Cheats");
			}
			GUI.backgroundColor = backgroundColor;
			GUI.contentColor = contentColor;
			GUI.color = color;
			GUI.matrix = matrix;
		}
	}

	// Token: 0x1700043C RID: 1084
	// (get) Token: 0x0600304A RID: 12362 RVA: 0x000242ED File Offset: 0x000224ED
	public static ModCheats instance
	{
		get
		{
			if (ModCheats._instance == null)
			{
				ModCheats._instance = UnityEngine.Object.FindObjectOfType<ModCheats>();
			}
			if (ModCheats._instance == null)
			{
				GameObject gameObject = new GameObject();
				ModCheats._instance = gameObject.AddComponent<ModCheats>();
				UnityEngine.Object.DontDestroyOnLoad(gameObject);
			}
			return ModCheats._instance;
		}
	}

	// Token: 0x04003809 RID: 14345
	private bool noclip;

	// Token: 0x0400380A RID: 14346
	private Vector3 noclipPos;

	// Token: 0x0400380B RID: 14347
	private bool cameraFollow;

	// Token: 0x0400380C RID: 14348
	private static FieldInfo cameraGameplayScene = typeof(CameraController).GetField("isGameplayScene", BindingFlags.Instance | BindingFlags.NonPublic);

	// Token: 0x0400380D RID: 14349
	private static ModCheats _instance;

	// Token: 0x0400380E RID: 14350
	public bool showSpeed;

	// Token: 0x0400380F RID: 14351
	private Rigidbody2D rb2d;

	// Token: 0x04003810 RID: 14352
	private BoxCollider2D collider2d;

	// Token: 0x04003811 RID: 14353
	public int SaveGeo;

	// Token: 0x04003812 RID: 14354
	public int SaveSoul;

	// Token: 0x04003813 RID: 14355
	public int SaveHealth;

	// Token: 0x04003814 RID: 14356
	public Vector3 SaveShadePos;

	// Token: 0x04003815 RID: 14357
	public Vector3 SavePos;

	// Token: 0x04003816 RID: 14358
	public bool SaveDeathstate;

	// Token: 0x04003817 RID: 14359
	public bool SaveMade;

	// Token: 0x04003818 RID: 14360
	public string SaveShadeScene;

	// Token: 0x04003819 RID: 14361
	public string SaveScene;
}
