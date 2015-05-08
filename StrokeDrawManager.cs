using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;

/// <summary>
/// Handles the strokes the player draws
/// </summary>
public class StrokeDrawManager : MonoBehaviour
{
	public GameObject PointPrefab; //the prefab containing the point sprite
	public Text HintTextBox; //text box to display the name of the character

	public float ToleranceDegrees = 25f; //how much leeway to give with the curve recognition

	private bool setUp = false;

	private KanjiVgParser KanjiVGGen;
	private TurnBasedSystemScript turnScript;

	private float Accuracy;
	private int NumberOfChars;
	private int CurrentCharacter;
	private int NumberOfStrokes;
	private int CurrentStroke;

	private List<GameObject> CurrentStrokePointsList;
	private List<GameObject> AllPointsList;
	private List<Vector2> Points;
	private float PointRadius;
	private GameObject Objects;

	private Animator StrokeAnimator;
	//animator transition variables hash code
	private int CharNoID;
	private int StrokeNoID;
	private int NextCharID;

	void Start()
	{
		GameObject kanjiManager = GameObject.Find("Kanji Manager");

		if (kanjiManager == null)
		{
			Debug.LogError("Kanji Manager not found");
		}

		KanjiVGGen = kanjiManager.GetComponent<KanjiVgParser>();
		Accuracy = 0f;

		turnScript = GameObject.Find("Turn Manager").GetComponent<TurnBasedSystemScript>();

		CurrentStrokePointsList = new List<GameObject>();
		AllPointsList = new List<GameObject>();
		Points = new List<Vector2>();

		PointRadius = PointPrefab.GetComponent<CircleCollider2D>().radius * 2;
		Objects = GameObject.Find("Objects");

		StrokeAnimator = GameObject.Find("Animation Manager").GetComponent<Animator>();

		CharNoID = Animator.StringToHash("characterNo");
		StrokeNoID = Animator.StringToHash("strokeNo");
		NextCharID = Animator.StringToHash("nextCharacter");
	}

	void Update()
	{
		if (KanjiVGGen.GenerationDone)
		{
			if (!setUp)
			{
				SetUpCharacters();
			}

			//if not touch was detected, check for mouse input
			if (Input.touchCount > 0)
			{
				HandleTouch();
			}
			else
			{
				HandleMouse();
			}
		}
	}

	/// <summary>
	/// Process when the user touches the screen
	/// </summary>
	private void HandleTouch()
	{
		Touch touch = Input.touches[0]; //handle only the first touch, disable multitouch

		if (touch.phase == TouchPhase.Began || touch.phase == TouchPhase.Moved)
		{
			HandleTouchStart(touch.position);
		}
		else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
		{
			HandleTouchEnd();
		}

	}

	/// <summary>
	/// Process when user draws using the mouse
	/// </summary>
	private void HandleMouse()
	{
		if (Input.GetMouseButton(0))
		{
			HandleTouchStart(Input.mousePosition);
		}
		else if (Input.GetMouseButtonUp(0))
		{
			HandleTouchEnd();
		}
	}

	/// <summary>
	/// Handle the drawing the stroke
	/// </summary>
	/// <param name="touchPos">where the user touched</param>
	private void HandleTouchStart(Vector3 touchPos)
	{
		Vector3 touchPosition = touchPos;
		touchPosition.z = -(Camera.main.transform.position.z);
		touchPosition = Camera.main.ScreenToWorldPoint(touchPosition);
		Vector3 lastPoint;

		if (Points.Count > 1)
		{
			lastPoint = Points[Points.Count - 1];
		}
		else
		{
			lastPoint = touchPosition;
		}

		InterpolateAddPoints(lastPoint, touchPosition);
	}

	/// <summary>
	/// Handle when the user stops drawing the stroke
	/// </summary>
	private void HandleTouchEnd()
	{
		AllPointsList.AddRange(CurrentStrokePointsList);

		CalculateAccuracy();

		Points.Clear();
		CurrentStrokePointsList.Clear();
	}

	/// <summary>
	/// Handle when the whole character was drawn
	/// </summary>
	private void HandleStrokeEnd()
	{
		for (int i = 0; i < AllPointsList.Count; i++)
		{
			Object.Destroy(AllPointsList[i]);
		}

		AllPointsList.Clear();
	}

	/// <summary>
	/// Add points in between the user's last touch position to make the line continuous
	/// </summary>
	/// <param name="lastPoint">the last touched point</param>
	/// <param name="currentPoint">the current point</param>
	private void InterpolateAddPoints(Vector3 lastPoint, Vector3 currentPoint)
	{
		float distance = Vector3.Distance(lastPoint, currentPoint);
		GameObject point;
		float radius = PointRadius/2;

		if (distance < radius)
		{
			Points.Add(new Vector2(currentPoint.x, currentPoint.y));
			point = Object.Instantiate(PointPrefab, currentPoint, Quaternion.identity) as GameObject;
			CurrentStrokePointsList.Add(point);
		}
		else
		{
			//using linear interpolation
			int subdivs = (int)(distance / radius);
			float t = 1.0f / subdivs;

			for (int j = 0; j < subdivs; j++)
			{
				Vector3 pointPos = lastPoint + t * j * (currentPoint - lastPoint);

				Points.Add(new Vector2(pointPos.x, pointPos.y));
				point = Object.Instantiate(PointPrefab, pointPos, Quaternion.identity) as GameObject;
				CurrentStrokePointsList.Add(point);
			}
		}
	}

	/// <summary>
	/// Compare the direction vectors from the reference curve and the drawn curve and stroke it
	/// </summary>
	private void CalculateAccuracy()
	{
		CheckStrokeAccuracy(CurrentCharacter, CurrentStroke);

		CurrentStroke++;

		if (StrokeAnimator != null)
		{
			StrokeAnimator.SetInteger(CharNoID, CurrentCharacter + 1);
			StrokeAnimator.SetInteger(StrokeNoID, CurrentStroke + 1);
		}

		if (CurrentStroke >= NumberOfStrokes)
		{
			CurrentStroke = 0;

			if (StrokeAnimator != null)
			{
				StrokeAnimator.SetTrigger(NextCharID);
			}

			HandleStrokeEnd();

			turnScript.currentState = TurnBasedSystemScript.BattleState.PlayerTurn;

			if (TouchCount.TotalTouchCounts == 0)
			{
				Accuracy = 0;
			}
			else
			{
				Accuracy = ((float)TouchCount.ValidTouchCount / (float)TouchCount.TotalTouchCounts);
			}

			turnScript.receivedAccuracyFromPointTouchManager = Accuracy;

			TouchCount.ValidTouchCount = 0;
			TouchCount.TotalTouchCounts = 0;
		}

		if (CurrentCharacter < NumberOfChars)
		{
			if (KanjiVGGen.DebugMode)
			{
				EnableCharacter(CurrentCharacter, CurrentStroke);
			}
		}
	}

	/// <summary>
	/// Compare the two strokes and count the valid curves drawn
	/// </summary>
	/// <param name="characterNo">character index</param>
	/// <param name="strokeNo">stroke index</param>
	private void CheckStrokeAccuracy(int characterNo, int strokeNo)
	{
		int validCount = 0;

		//get the direction vector list of the two curves
		List<Vector2> refDirections = KanjiVGGen.ReturnDirections(characterNo, strokeNo);
		List<Vector2> calcDirections = GenerateDirections(refDirections.Count);

		int count = refDirections.Count;

		for (int i = 0; i < count; i++)
		{
			if (i >= calcDirections.Count)
			{
				break;
			}

			Vector2 refDirVec = refDirections[i];
			Vector2 calcDirVec = calcDirections[i];

			//find the direction of the vector in degrees
			float refAngle = Mathf.Atan2(refDirVec.y, refDirVec.x) * Mathf.Rad2Deg;
			float calcAngle = Mathf.Atan2(calcDirVec.y, calcDirVec.x) * Mathf.Rad2Deg;

			//add the tolerance to the angle
			float minAngle = refAngle - ToleranceDegrees;
			float maxAngle = refAngle + ToleranceDegrees;

			//compare with the reference curve
			if (calcAngle > minAngle && calcAngle < maxAngle)
			{
				validCount++;
			}
		}

		TouchCount.ValidTouchCount += validCount;
		TouchCount.TotalTouchCounts += count;
	}

	/// <summary>
	/// Generate list of direction vectors for the drawn curve
	/// </summary>
	/// <param name="refCount">the number of vectors in the reference curve</param>
	/// <returns>list of direction vectors</returns>
	private List<Vector2> GenerateDirections(int refCount)
	{
		List<Vector2> newDirs = new List<Vector2>();

		if (refCount > Points.Count)
		{
			Debug.LogWarning("Drawn points count less than direction list count");
		}
		else
		{
			//find how to group the points based on the number of vectors present in the reference curve
			//to ensure that the number of vectors is the same on both curves
			int pointsPerGroup = Mathf.FloorToInt((float)Points.Count / (float)refCount);

			//this might result in some points being left out
			for (int i = 0; i < refCount; i++)
			{
				Vector2 startPoint = Points[i * pointsPerGroup];
				Vector2 endPoint;

				int nextIndex = ((i + 1) * pointsPerGroup) - 1;

				if (nextIndex >= Points.Count)
				{
					endPoint = Points[Points.Count - 1];
				}
				else
				{
					endPoint = Points[nextIndex];
				}

				Vector2 direction = new Vector2(endPoint.x - startPoint.x, endPoint.y - startPoint.y);
				newDirs.Add(direction);
			}
		}

		return newDirs;
	}

	/// <summary>
	/// Initial setup
	/// </summary>
	private void SetUpCharacters()
	{
		NumberOfChars = KanjiVGGen.NumberOfCharacters();
		CurrentCharacter = 0;
		CurrentStroke = 0;
		NumberOfStrokes = KanjiVGGen.NumberOfStrokes(CurrentCharacter);
		setUp = true;
		HintTextBox.text = KanjiVGGen.ReturnCharacterRomaji(CurrentCharacter);

		if (StrokeAnimator != null)
		{
			StrokeAnimator.SetInteger(CharNoID, CurrentCharacter + 1);
			StrokeAnimator.SetInteger(StrokeNoID, CurrentStroke + 1);
		}

		if (KanjiVGGen.DebugMode)
		{
			EnableCharacter(CurrentCharacter, CurrentStroke);
		}
	}

	/// <summary>
	/// getter for the current character
	/// </summary>
	/// <returns>index of the first character</returns>
	public int ReturnCharacter()
	{
		return CurrentCharacter;
	}

	/// <summary>
	/// setter for the character
	/// called by the turn based battle system to draw a certain character
	/// </summary>
	/// <param name="characterNumber">the index of the character to draw</param>
	public void SetCharacter(int characterNumber)
	{
		if (KanjiVGGen != null)
		{
			CurrentCharacter = characterNumber;
			CurrentStroke = 0;
			NumberOfStrokes = KanjiVGGen.NumberOfStrokes(CurrentCharacter);

			if (StrokeAnimator != null)
			{
				StrokeAnimator.SetInteger(CharNoID, CurrentCharacter + 1);
				StrokeAnimator.SetInteger(StrokeNoID, CurrentStroke + 1);
			}

			HintTextBox.text = KanjiVGGen.ReturnCharacterRomaji(CurrentCharacter);

			if (KanjiVGGen.DebugMode)
			{
				EnableCharacter(CurrentCharacter, CurrentStroke);
			}

		}
	}

	/// <summary>
	/// if debug mode, enable the points for the required character and stroke and turn off the rest
	/// </summary>
	/// <param name="characterNo">character index</param>
	/// <param name="strokeNo">stroke index</param>
	private void EnableCharacter(int characterNo, int strokeNo)
	{
		for (int i = 0; i < NumberOfChars; i++)
		{
			if (i == characterNo)
			{
				HintTextBox.text = Objects.transform.GetChild(i).name;
				NumberOfStrokes = Objects.transform.GetChild(i).childCount;

				for (int j = 0; j < NumberOfStrokes; j++)
				{
					GameObject currentStroke = Objects.transform.GetChild(i).GetChild(j).gameObject;

					if (j == strokeNo)
					{
						currentStroke.SetActive(true);
					}
					else
					{
						currentStroke.SetActive(false);
					}
				}
			}
			else
			{
				int strokeCount = Objects.transform.GetChild(i).childCount;

				for (int j = 0; j < strokeCount; j++)
				{
					Objects.transform.GetChild(i).GetChild(j).gameObject.SetActive(false);
				}
			}
		}

	}
}
