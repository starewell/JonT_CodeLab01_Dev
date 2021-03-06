using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
//Scraps from the TileFlip class to utilize for button mechanics... Just stole them from the UI.Button class
[RequireComponent(typeof(Button))]
[RequireComponent(typeof(Animator))]
public class TileButton : Interactable {

	public AudioSource audioSource;
	Animator anim;
	Button button; //...Passing executable functions through unity button rather than hardcoded reference... why is this bad
	[SerializeField] Animator padlock;

	public override void Start() {
        base.Start();

		anim = GetComponent<Animator>();
		anim.SetBool("Destroy", false);

		button = GetComponent<Button>();

		if (active) padlock.gameObject.SetActive(false);
		else padlock.gameObject.SetActive(true);
	}
//When base class detects an interact function
    protected override void Interact() {
        base.Interact();

		StartCoroutine(FlipTile());
    }
//Animate tile and invoke event for result
	public IEnumerator FlipTile() {
		active = true;
		StartCoroutine(hover.Deactivate());
		anim.SetBool("Flip", true);
		//yield return new WaitForSeconds(anim.GetCurrentAnimatorClipInfo(0)[0].clip.length);
		//Hardcoded delay bc I couldn't figure out delaying by current animation clip ^^^
		yield return new WaitForSeconds(.25f);
		audioSource.Play();
		anim.SetBool("Flip", false);
		button.onClick.Invoke();
	}
//Set the button as active or inactive and toggle anim state
	public void ChangeLockState(bool state) { 
		if (state) {
			padlock.gameObject.SetActive(state);
			padlock.SetBool("Unlock", !state);
			active = !state;
		} else {
			padlock.SetBool("Unlock", !state);			
			Debug.Log("Playing " + audioSource);
			active = !state;
		}
	}

	public void UnlockOnStart() {
		padlock.gameObject.SetActive(false);
		active = true;
	}
}
