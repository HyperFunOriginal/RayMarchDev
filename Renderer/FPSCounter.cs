using System.Collections;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public class FPSCounter : MonoBehaviour {

	public float fps;
	private Text txt;
	internal float timestep;
	internal int frameCount;
	// Use this for initialization
	void Start () {
		fps = 60f;
		timestep = 1;
		txt = gameObject.GetComponent<Text>();
		txt.text = "";
		StartCoroutine(FPSCount());
	}
	
	public IEnumerator FPSCount()
    {
		while (true)
        {
			yield return new WaitForSecondsRealtime(timestep);
			fps = frameCount / timestep;
			frameCount = 0;
			timestep = Mathf.Clamp(20f / fps, 0.5f, 1f);
			txt.text = "FPS: " + Math.Round(fps, 1);
        }
    }

	// Update is called once per frame
	void Update () {
		frameCount++;
	}
}
