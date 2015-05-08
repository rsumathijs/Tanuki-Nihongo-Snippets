using UnityEngine;
using System.Collections.Generic;
using System.Xml;
using System.IO;

/// <summary>
/// Class to handle parsing the XML into Bezier curve data 
/// </summary>
public class XMLParser
{
    public List<CharacterVO> Characters { get; set; }

    private XmlDocument XMLDoc;

    public XMLParser()
    {
        XMLDoc = new XmlDocument();
        Characters = new List<CharacterVO>();
    }

	/// <summary>
	/// Load the contents of the XML file into the XML Document
	/// </summary>
	/// <param name="xmlFileContents">contents of the XML file for the stage</param>
	/// <returns>true if the load was successful, false otherwise</returns>
    public bool LoadXMLFile(string xmlFileContents)
    {
        if (xmlFileContents.Length != 0)
        {
            XMLDoc.LoadXml(xmlFileContents);
            return true;
        }
        else
        {
            Debug.LogError("XML file is empty");
            return false;
        }
    }

	/// <summary>
	/// Parse the XML Document into property bags
	/// </summary>
    public void ParseXML()
    {
        //get all nodes with kanji tag
        XmlNodeList kanjiNodes = XMLDoc.GetElementsByTagName("kanji");

        foreach (XmlNode kanjiNode in kanjiNodes)
        {
			//get all the g tags under the kanji tag
            XmlNodeList gNodes = kanjiNode.ChildNodes; 

            foreach (XmlNode gNode in gNodes)
            {
				//the name attribute is the name of the character
                CharacterVO character = new CharacterVO();
                character.Romaji = gNode.Attributes["name"].Value;
                character.Strokes = new Dictionary<int, string>();

				//get the path tags under the g tag
                XmlNodeList pathNodes = gNode.ChildNodes;

                int strokeNo = 0;
                foreach (XmlNode path in pathNodes)
                {
					//d attribute corresponds to the bezier curve data
                    character.Strokes.Add(++strokeNo, path.Attributes["d"].Value);
                }

                Characters.Add(character);
            }
        }
    }
}
