using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Class that handles the parsing from XML to SVG to Curve data
/// </summary>
public class KanjiVgParser : MonoBehaviour
{
    public TextAsset StageXmlData; //the XML file for the stage
	
    public int NumberOfSubdivisions = 2; //subdivision to make for the bezier curve
	public int PointsPerGroup = 5; //the number of points to group together when calculating the direction of the curve

	public bool DebugMode = false; //whether to draw the debug curve
    public float DrawScale = 0.65f; //scale of the point
    public GameObject DrawPointPrefab; //prefab containing the sprite to use to draw the debug curve

    [HideInInspector]
    public bool GenerationDone = false;

    private XMLParser XmlParser;
    private List<CharacterInfoVO> ParsedCharacters;
    private List<string> CharacterNames;
    private float PointRadius;

    void Start()
    {
		//parse the XML to SVG data
        XmlParser = new XMLParser();
        XmlParser.LoadXMLFile(StageXmlData.text);
        XmlParser.ParseXML();

		//get the radius of the stroke's point (used as basis for distance between points)
        GameObject PointPrefab = GameObject.Find("Draw Manager").GetComponent<StrokeDrawManager>().PointPrefab;

        if (PointPrefab != null)
        {
            PointRadius = PointPrefab.GetComponent<CircleCollider2D>().radius * 2;
        }
        else
        {
            Debug.LogWarning("Point Prefab not set in DrawManager. Default radius taken for point");
            PointRadius = 1f;
        }

		//convert the SVG data into points on the Bezier curve
        ParseVGData();

		//interpolate between the points to smoothen it
        InterpolatePoints();

		//draw the points in debug mode
		if (DebugMode)
		{
			DrawPoints();
		}

		//calculate the stroke direction for the curves
        CalculateDirections();

		//set value to true, so other dependent components can proceed with their work
        GenerationDone = true;
    }

	/// <summary>
	/// Method to handle the conversion of SVG data into points
	/// </summary>
    private void ParseVGData()
    {
        ParsedCharacters = new List<CharacterInfoVO>();
        CharacterNames = new List<string>();

        foreach (CharacterVO character in XmlParser.Characters)
        {
            CharacterInfoVO charInfo = new CharacterInfoVO();

            charInfo.Romaji = character.Romaji;

            //splitting the romaji by space and getting just the first part (required for audio clips)
            CharacterNames.Add(character.Romaji.Split(' ')[0]);

            Dictionary<int, List<Vector2>> parsedStrokes = new Dictionary<int, List<Vector2>>();

			//convert each SVG string into a bezier curve
            foreach (int key in character.Strokes.Keys)
            {
                List<Vector2> strokePoints = ParseVGString(character.Strokes[key]);
                parsedStrokes.Add(key, strokePoints);
            }

            charInfo.Strokes = parsedStrokes;

			ParsedCharacters.Add(charInfo);
        }
    }

	/// <summary>
	/// Convert a Bezier curve stored as a string into a set of points
	/// </summary>
	/// <param name="bezierCurveData">bezier curve data</param>
	/// <returns>list of coordinates for the points</returns>
    private List<Vector2> ParseVGString(string bezierCurveData)
    {
        List<Vector2> pointsList = new List<Vector2>();

		//fix the incoming string
        bezierCurveData = bezierCurveData.Replace(" ", "");
        bezierCurveData = bezierCurveData.Replace("-", ",-");
        bezierCurveData = bezierCurveData.Replace("c", ",c");
        bezierCurveData = bezierCurveData.Replace("c,-", "c-");
        bezierCurveData = bezierCurveData.Replace("C", ",C");
        bezierCurveData = bezierCurveData.Replace("C,-", "C-");

		//points are comma delimited
        string[] splitPoints = bezierCurveData.Split(',');
        int index = 0;

        Vector2 lastControlPoint = Vector2.zero;

        while (index < splitPoints.Length)
        {
            string firstChar = splitPoints[index].Substring(0, 1); //get the first character

            switch (firstChar)
            {
            case "M": //start of curve
            {
				//remove the letter from the number
                string corrected = splitPoints[index].Substring(1);
                float x = float.Parse(corrected);
                float y = float.Parse(splitPoints[index + 1]);

                lastControlPoint = new Vector2(x, y);
                pointsList.Add(lastControlPoint);
                index += 2;
            }
            break;

            case "c": //relative cubic bezier curve
            {
                string corrected = splitPoints[index].Substring(1);
                float x1 = float.Parse(corrected) + lastControlPoint.x;
                float y1 = float.Parse(splitPoints[index + 1]) + lastControlPoint.y;
                float x2 = float.Parse(splitPoints[index + 2]) + x1;
                float y2 = float.Parse(splitPoints[index + 3]) + y1;
                float x3 = float.Parse(splitPoints[index + 4]) + x2;
                float y3 = float.Parse(splitPoints[index + 5]) + y2;

                pointsList.AddRange(BuildBezierCurve(lastControlPoint.x, lastControlPoint.y, x1, y1, x2, y2, x3, y3));

                lastControlPoint = new Vector2(x3, y3);
                index += 6;
            }
            break;

            case "C": //absolute cubic bezier curve
            {
                string corrected = splitPoints[index].Substring(1);
                float x1 = float.Parse(corrected);
                float y1 = float.Parse(splitPoints[index + 1]);
                float x2 = float.Parse(splitPoints[index + 2]);
                float y2 = float.Parse(splitPoints[index + 3]);
                float x3 = float.Parse(splitPoints[index + 4]);
                float y3 = float.Parse(splitPoints[index + 5]);

                pointsList.AddRange(BuildBezierCurve(lastControlPoint.x, lastControlPoint.y, x1, y1, x2, y2, x3, y3));

                lastControlPoint = new Vector2(x3, y3);
                index += 6;
            }
            break;

            default:
                Debug.LogWarning("Unknown first character : " + firstChar);
                break;
            }
        }

        return pointsList;
    }

	/// <summary>
	/// Method to construct a cubic bezier curve from the set of points
	/// Adapted from a C++ implementation of the same (http://antigrain.com/research/bezier_interpolation/index.html#PAGE_BEZIER_INTERPOLATION)
	/// </summary>
	/// <param name="x1">start point x</param>
	/// <param name="y1">start point y</param>
	/// <param name="x2">control point 1 x</param>
	/// <param name="y2">control point 1 y</param>
	/// <param name="x3">control point 2 x</param>
	/// <param name="y3">control point 2 y</param>
	/// <param name="x4">end point x</param>
	/// <param name="y4">end point y</param>
	/// <returns>list of points representing the bezier curve</returns>
    List<Vector2> BuildBezierCurve(float x1, float y1, float x2, float y2, float x3, float y3, float x4, float y4)
    {
        List<Vector2> pointsList = new List<Vector2>();

        float subdivStep = 1.0f / (float)(NumberOfSubdivisions + 1);
        float subdivStep2 = subdivStep * subdivStep;
        float subdivStep3 = subdivStep * subdivStep * subdivStep;

        float pre1 = 3.0f * subdivStep;
        float pre2 = 3.0f * subdivStep2;
        float pre4 = 6.0f * subdivStep2;
        float pre5 = 6.0f * subdivStep3;

        float tmp1x = x1 - x2 * 2.0f + x3;
        float tmp1y = y1 - y2 * 2.0f + y3;

        float tmp2x = (x2 - x3) * 3.0f - x1 + x4;
        float tmp2y = (y2 - y3) * 3.0f - y1 + y4;

        float fx = x1;
        float fy = y1;

        float dfx = (x2 - x1) * pre1 + tmp1x * pre2 + tmp2x * subdivStep3;
        float dfy = (y2 - y1) * pre1 + tmp1y * pre2 + tmp2y * subdivStep3;

        float ddfx = tmp1x * pre4 + tmp2x * pre5;
        float ddfy = tmp1y * pre4 + tmp2y * pre5;

        float dddfx = tmp2x * pre5;
        float dddfy = tmp2y * pre5;

        int step = NumberOfSubdivisions;

        while (step-- > 0)
        {
            fx += dfx;
            fy += dfy;
            dfx += ddfx;
            dfy += ddfy;
            ddfx += dddfx;
            ddfy += dddfy;
            pointsList.Add(new Vector2(fx, fy));
        }

        pointsList.Add(new Vector2(x4, y4));

        return pointsList;
    }

	/// <summary>
	/// Method to interpolate between two points on a bezier curve
	/// </summary>
    void InterpolatePoints()
    {
        foreach (CharacterInfoVO character in ParsedCharacters)
        {
            foreach (List<Vector2> list in character.Strokes.Values)
            {
                List<Vector2> newList = new List<Vector2>();

                for (int i = 0; i < list.Count - 1; i++)
                {
                    float distance = Vector2.Distance(list[i], list[i + 1]);

                    if (distance < PointRadius)
                    {
                        newList.Add(list[i]);
                    }
                    else
                    {
                        int subdivs = (int)(distance / PointRadius);
                        float t = 1.0f / subdivs;

                        for (int j = 0; j < subdivs; j++)
                        {
                            Vector2 point = list[i] + t * j * (list[i + 1] - list[i]);
                            newList.Add(point);
                        }
                    }
                }

                newList.Add(list[list.Count - 1]);
                list.Clear();
                list.AddRange(newList);

            }
        }
    }

	/// <summary>
	/// Getter for the list of characters
	/// </summary>
	/// <returns>list of characters</returns>
    public List<string> GetCharacterList()
    {
        return CharacterNames;
    }

	/// <summary>
	/// Find the direction vectors for the curve
	/// </summary>
    private void CalculateDirections()
    {
        foreach (CharacterInfoVO character in ParsedCharacters)
        {
            Dictionary<int, List<Vector2>> directions = new Dictionary<int, List<Vector2>>();

            foreach (int key in character.Strokes.Keys)
            {
                List<Vector2> points;
                character.Strokes.TryGetValue(key, out points);

                List<Vector2> directionVectors = CreateDirections(points);
                directions.Add(key, directionVectors);
            }

            character.Directions = directions;
        }
    }

	/// <summary>
	/// Create a list of points representing the set of direction vectors for the curve
	/// </summary>
	/// <param name="points">the curve points</param>
	/// <returns>the list of direction vectors for the curve</returns>
    List<Vector2> CreateDirections(List<Vector2> points)
    {
        List<Vector2> directions = new List<Vector2>();

        if (points.Count < PointsPerGroup)
        {
            Debug.LogWarning("Number of points on curve is less than points per group");
        }

		//find number of direction vectors to make, that is, group the points into a vector
        int groupCount = Mathf.CeilToInt((float)points.Count / (float)PointsPerGroup);

        for (int i = 0; i < groupCount; i++)
        {
            Vector2 startPoint = points[i * PointsPerGroup];
            Vector2 endPoint;

			//get the index of the point at the end of the group
            int nextIndex = ((i + 1) * PointsPerGroup) - 1;

			//if the index exceeds the total number of points, use the last point in the curve as the end point
            if (nextIndex >= points.Count)
            {
                endPoint = points[points.Count - 1];
            }
            else
            {
                endPoint = points[nextIndex];
            }

			//make the direction vector
            Vector2 direction = new Vector2(endPoint.x - startPoint.x, (endPoint.y - startPoint.y) * -1);
            directions.Add(direction);
        }

        return directions;
    }

	/// <summary>
	/// return the list of direction vectors for the chracter and stroke
	/// </summary>
	/// <param name="charNo">the character index</param>
	/// <param name="strokeNo">the stroke index</param>
	/// <returns>list of direction vectors</returns>
    public List<Vector2> ReturnDirections(int charNo, int strokeNo)
    {
        List<Vector2> dirs;
        ParsedCharacters[charNo].Directions.TryGetValue(strokeNo + 1, out dirs);

        return dirs;
    }

	/// <summary>
	/// return the total number of strokes in the character
	/// </summary>
	/// <param name="charNo">the index of the character</param>
	/// <returns>number of strokes in the character</returns>
    public int NumberOfStrokes(int charNo)
    {
        return ParsedCharacters[charNo].Directions.Count;
    }

	/// <summary>
	/// return the total number of characters
	/// </summary>
	/// <returns>number of characters</returns>
    public int NumberOfCharacters()
    {
        return ParsedCharacters.Count;
    }

	/// <summary>
	/// return the name of the character
	/// </summary>
	/// <param name="charNo">character index</param>
	/// <returns>name of the character</returns>
    public string ReturnCharacterRomaji(int charNo)
    {
        return ParsedCharacters[charNo].Romaji;
    }

	//draw the debug curve
    void DrawPoints()
    {
        Transform ObjectsGO = GameObject.Find("Objects").transform; //where to parent the points to

        foreach (CharacterInfoVO character in ParsedCharacters)
        {
            int i = 0;

			//create a new game object with the name of the character and parent each stroke it
            GameObject Points = new GameObject(character.Romaji);
            Points.transform.parent = ObjectsGO;
			Points.transform.localPosition = Vector3.zero;

            foreach (List<Vector2> list in character.Strokes.Values)
            {
				//create a new game object with the stroke number and parent each stroke's point to it
                GameObject stroke = new GameObject((++i).ToString());
                stroke.transform.parent = Points.transform;
				stroke.transform.localPosition = Vector3.zero;

				//instantiate a point and parent it to the stroke
                foreach (Vector2 point in list)
                {
                    Vector2 position = new Vector2(point.x * DrawScale, -point.y * DrawScale);
                    GameObject pointGO = Instantiate(DrawPointPrefab, position, Quaternion.identity) as GameObject;
                    pointGO.transform.parent = stroke.transform;
					pointGO.transform.localPosition = Vector3.zero;
                    pointGO.transform.localPosition = position;
                }
            }
        }
    }
}
