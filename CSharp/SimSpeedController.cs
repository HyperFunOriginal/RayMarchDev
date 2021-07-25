using System.Collections;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Raymarch;

public class SimSpeedController : MonoBehaviour {

	Text txt;
	Render raymarcher;
	// Use this for initialization
	void Start()
	{
		txt = gameObject.GetComponent<Text>();
		raymarcher = GetRenderer.GetRendererMethod();
	}

	// Update is called once per frame
	void Update () {
		if (raymarcher.builtinControls)
		{
			if (Input.GetKey(KeyCode.R))
				raymarcher.simSpeed *= 0.99f;

			if (Input.GetKey(KeyCode.T))
				raymarcher.simSpeed *= 1.015f;
		}
		float rounded = Mathf.Pow(10f, (int)Mathf.Log10(raymarcher.simSpeed));
		txt.text = "Sim. Speed: " + System.Math.Round(raymarcher.simSpeed * 0.02f / rounded, 4) * rounded + "x";
	}
}
