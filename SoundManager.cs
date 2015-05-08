using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Class that handles loading and playing of sounds
/// Dependent on the Kanji VG Parser class to get list of characters
/// </summary>
public class SoundManager : MonoBehaviour
{
	private KanjiVgParser KanjiParser;
	private List<string> Characters;

	private Dictionary<string, AudioClip> AudioClipList;
	private AudioSource SoundSource;

	private bool SoundLoadDone = false;

	void Start()
	{
		KanjiParser = GameObject.Find("Kanji Manager").GetComponent<KanjiVgParser>();
		SoundSource = GetComponent<AudioSource>();
	}

	void Update()
	{
		//Load sound once
		//Done in Update since it has to wait till the Kanji VG Parser is done
		if (KanjiParser.GenerationDone && !SoundLoadDone)
		{
			Characters = KanjiParser.GetCharacterList();

			AudioClipList = new Dictionary<string, AudioClip>();

			foreach (string character in Characters)
			{
				AudioClip clip = Resources.Load(character, typeof(AudioClip)) as AudioClip;
				AudioClipList.Add(character, clip);
			}

			SoundLoadDone = true;

			//the turn based battle system is dependent on this
			//enabling it when the sound is done loading
			GameObject.Find("Turn Manager").GetComponent<TurnBasedSystemScript>().enabled = true;
		}
	}

	/// <summary>
	/// Play the sound clip corresponding to the name of the character
	/// </summary>
	/// <param name="character">name of the sound clip</param>
	public void PlaySound(string character)
	{
		if (SoundLoadDone)
		{
			SoundSource.Stop();
			AudioClip clip = AudioClipList[character];

			if (clip != null)
			{
				SoundSource.clip = clip;
				SoundSource.Play();
			}
			else
			{
				Debug.LogWarning("Invalid sound name :" + character);
			}
		}
		else
		{
			Debug.Log("Still loading");
		}
	}

	/// <summary>
	/// Helper method to play the sound with the character's index in the turn based battle system
	/// </summary>
	/// <param name="index">the character number</param>
	public void PlaySound(int index)
	{
		PlaySound(Characters[index]);
	}
}