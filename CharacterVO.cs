using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Property Bag class to store the unprocessed Curve information
/// </summary>
public class CharacterVO
{
	/// <summary>
	/// Override for debug
	/// </summary>
	/// <returns>the string version of the class</returns>
    public override string ToString()
    {
        return Romaji + " " + Strokes.ToString();
    }

    public string Romaji { get; set; } //the name of the character
	public Dictionary<int, string> Strokes { get; set; } //the list of strokes in the character
}
