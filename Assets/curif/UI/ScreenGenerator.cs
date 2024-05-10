using UnityEngine;
using System.Collections.Generic;
using System;
using System.Text;
using System.Reflection;

public class ScreenGenerator : MonoBehaviour
{
    // The texture that represents a matrix of characters, each character is 8x8 pixels
    public Texture2D c64Font;

    public int CharactersWidth = 40;
    public int CharactersHeight = 25;
    public int TextureWidth;
    public int TextureHeight;
    public Color32 BackgroundColor;
    public Color32 CenteredAreaColor;

    //public string ShaderName = "crt";
    [SerializeField]
    public Dictionary<string, string> ShaderConfig = new Dictionary<string, string>();

    // The list that stores the pixels for each character
    private List<Color32[]> charPixels;

    // The string that contains the list of characters in the same order as the font texture
    // the arroba means "no replace"
    private string characterListOrder = "@ABCDEFGHIJKLMNOPQRSTUVWXYZ@@@@+ !\"#$%&'()*+,-./0123456789:;<=>?@@|@@@_@@@@@@\\";
    private string characterListOrderAlternate = "@abcdefghijklmnopqrstuvwxyz@@@@+ !\"#$%&'()*+,-./0123456789:;<=>?@@|@@@_@@@@@@\\";
    private int characterPositionForNotFound = 32; //" "

    // The texture that represents the screen, it has 40x25 characters of capacity
    private Texture2D c64Screen;
    private Color32[] colorsBackgroundMatrix;
    //private Color32[] colorsCenteredAreaMatrix;

    private int ScreenWidth; // Width of the target texture
    private int ScreenHeight; // Height of the target texture
    private int centerStartX;
    private int centerStartY;

    private bool needsDraw = false;

    //Renderer
    private ShaderScreenBase shader;

    public Texture2D Screen
    {
        get
        {
            return c64Screen;
        }
    }

    // The method that runs at the start of the game
    private void Start()
    {
        createTexture();
    }

    private Texture2D createTexture()
    {
        if (c64Screen != null)
            return c64Screen;

        ScreenWidth = CharactersWidth * 8;  // Width of the target texture
        ScreenHeight = CharactersHeight * 8; // Height of the target texture

        //characters
        // Initialize the list
        charPixels = new List<Color32[]>();

        // Retrieve all pixels from the texture
        Color32[] allPixels = c64Font.GetPixels32();

        // Loop through all the characters in the font texture
        for (int i = 0; i < 256; i++)
        {
            // Calculate the pixel coordinates for the source texture
            int srcX = (i % 32) * 8;
            int srcY = (7 - i / 32) * 8;

            Color32[] charPixelsArray = new Color32[64]; // 8x8 characters

            // Extract the pixels for the current character
            for (int row = 0; row < 8; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    int pixelIndex = ((srcY + row) * c64Font.width + (srcX + col));
                    charPixelsArray[row * 8 + col] = allPixels[pixelIndex];
                }
            }

            charPixels.Add(charPixelsArray);
        }

        // Create an array of colors to fill the background texture
        colorsBackgroundMatrix = new Color32[ScreenWidth * ScreenHeight];
        Array.Fill(colorsBackgroundMatrix, CenteredAreaColor);

        // Calculate the width and height of the centered area
        int centeredWidth = CharactersWidth * 8;
        int centeredHeight = CharactersHeight * 8;

        // Calculate the position of the centered area based on the whole texture size
        centerStartX = (TextureWidth - centeredWidth) / 2;
        centerStartY = (TextureHeight - centeredHeight) / 2;

        // Create the target texture with the specified width and height, no mips
        c64Screen = new Texture2D(TextureWidth, TextureHeight, TextureFormat.RGBA32, false);
        c64Screen.name = "c64";

        //c64Screen.wrapMode = TextureWrapMode.Clamp;
        c64Screen.filterMode = FilterMode.Bilinear;
        c64Screen.anisoLevel = 0;
        
        setBackgroundColor();
        Clear();

        return c64Screen;
    }

    public void ActivateShader(ShaderScreenBase changeShader)
    {
        if (c64Screen == null)
            createTexture();

        shader = changeShader;
        shader.Activate(c64Screen);
    }
    
    public void Update()
    {
        shader?.Update();
        return;
    }

    private ScreenGenerator setBackgroundColor()
    {
        if (c64Screen != null)
        {
            // Create a new array of color data for the texture
            Color32[] pixels = c64Screen.GetPixels32();
            Array.Fill(pixels, BackgroundColor);

            /*
            // Set the color for each pixel in the texture
            for (int i = 0; i < TextureWidth * TextureHeight; i++)
            {
                pixels[i] = BackgroundColor;
            }
            */

            // Apply the modified colors back to the texture
            c64Screen.SetPixels32(pixels);

            needsDraw = true;
        }
        return this;
    }

    // copies the texture to the gpu if it was modified
    public ScreenGenerator DrawScreen()
    {
        if (needsDraw)
        {
            c64Screen.Apply();
            needsDraw = false;
        }
        return this;
    }

    // The method that prints a single character to the screen
    public ScreenGenerator PrintChar(int x, int y, int charNum)
    {
        if (c64Screen == null)
            return this;

        // Check if the coordinates and the character number are valid
        if (x < 0 || x >= CharactersWidth || y < 0 || y >= CharactersHeight || charNum < 0 || charNum >= 256)
        {
            ConfigManager.WriteConsoleError($"[ScreenGenerator.PrintChar] Invalid parameters x,y: ({x},{y}), charNum: {charNum}");
            return this;
        }

        // Calculate the pixel coordinates for the destination texture within the centered area
        int destX = (x * 8) + centerStartX;
        //int destY = (TextureHeight - ((y + 1) * 8)) - centerY; // Subtract y from CharactersHeight and centerY to flip the origin
        int destY = centerStartY + (CharactersHeight - (y + 1)) * 8; // Add centerY and adjust the y calculation to be relative to the top of the centered area

        // Copy the pixels from the list to the screen texture
        c64Screen.SetPixels32(destX, destY, 8, 8, charPixels[charNum]);

        needsDraw = true;

        return this;
    }

    // The method that prints a string of characters to the screen
    public ScreenGenerator Print(int x, int y, string text, bool inverted = false)
    {
        if (c64Screen == null)
            return this;

        // Check if the coordinates are valid
        if (x < 0 || x >= CharactersWidth || y < 0 || y >= CharactersHeight)
        {
            ConfigManager.WriteConsoleError($"[ScreenGenerator.Print] Invalid parameters for Print method, x,y: ({x},{y})");
            return this;
        }

        // Loop through all the characters in the text string
        int charpos = x;
        int i = 0;
        while (i < text.Length)
        {
            // Check for escape character and make sure it's not the last char
            if (text[i] == '\\' && i <= text.Length - 1) 
            {
                i++;
                if (text[i] == '\\') 
                {
                    PrintCharPosition(text[i], ref charpos, ref y);
                    i++;
                }
                else
                {
                    StringBuilder strIndex = new StringBuilder();
                    // Find the end of the number sequence
                    while (i <= text.Length - 1 && char.IsDigit(text[i]) && strIndex.Length < 4)
                    {
                        strIndex.Append(text[i]);
                        i++;
                    }

                    int index;
                    if (strIndex.Length > 0)
                    {
                        if (!int.TryParse(strIndex.ToString(), out index))
                            index = characterPositionForNotFound;
                        else if (index >= 256)
                            index = characterPositionForNotFound;
                    }
                    else
                    {
                        index = 77; // "\"
                    }

                    PrintCharPosition(index, ref charpos, ref y);
                }
            }
            else
            {
                // Find the index of the character in the character list order string
                int index = characterListOrder.IndexOf(text[i]);
                if (index == -1)
                    index = characterListOrderAlternate.IndexOf(text[i]);
                if (index == -1)
                    index = characterPositionForNotFound;
                if (index > 128)
                    index = characterPositionForNotFound;
                // Print the character to the screen using PrintChar method with inversion flag
                index = inverted ? index + 128 : index;
                PrintCharPosition(index, ref charpos, ref y);
                i++;
            }

            //ConfigManager.WriteConsole($"[ScreenGenerator.Print]char:[{c}] position: {index}");
        }

        return this;
    }

    private void PrintCharPosition(int charNum, ref int x, ref int y)
    {
        PrintChar(x, y, charNum);

        x++;
        if (x >= CharactersWidth)
        {
            x = 0;
            y++;
        }
    }

    // The method that clears the screen with a given color or cyan by default
    public ScreenGenerator Clear()
    {
        // Fill the screen texture with the given color
        if (c64Screen == null)
            createTexture();

        c64Screen.SetPixels32(centerStartX, centerStartY, 
                                ScreenWidth, ScreenHeight, 
                                colorsBackgroundMatrix);
        needsDraw = true;
        return this;
    }

    // The method that prints a string of characters to the center of the X axis
    public ScreenGenerator PrintCentered(int y, string text, bool inverted = false)
    {
        if (c64Screen == null)
            return this;

        // Check if the y coordinate is valid
        if (y < 0 || y >= CharactersHeight)
        {
            ConfigManager.WriteConsoleError($"[ScreenGenerator.PrintCentered] Invalid parameters y: ({y})");
            return this;
        }

        // Calculate the x coordinate that will center the text
        int x = (CharactersWidth - text.Length) / 2;

        // Print the text using the Print method with the calculated x coordinate
        Print(x, y, text, inverted);

        return this;
    }


    // The method that prints the same character in a line
    public ScreenGenerator PrintLine(int y, bool inverted, char c = '-')
    {
        if (c64Screen == null)
            return this;
        // Check if the y coordinate is valid
        if (y < 0 || y >= CharactersHeight)
        {
            ConfigManager.WriteConsoleError($"[ScreenGenerator.PrintLine] Invalid parameters y: ({y})");
            return this;
        }

        // Create a string of 40 characters using the given character
        string text = new string(c, CharactersWidth - 1);

        // Print the text using the Print method with the x coordinate of 0
        Print(0, y, text, inverted);
        return this;
    }

}
