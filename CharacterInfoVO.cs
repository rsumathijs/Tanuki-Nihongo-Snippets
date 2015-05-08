using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Property Bag class to store the processed Curve information
/// </summary>
public class CharacterInfoVO
{
    public string Romaji { get; set; } //the name of the character
    public Dictionary<int, List<Vector2>> Strokes { get; set; } //the list of strokes in the character
    public Dictionary<int, List<Vector2>> Directions { get; set; } //the list of direction vectors for each stroke in the character
}
