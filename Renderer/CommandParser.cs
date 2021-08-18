using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;

public class CommandParser : MonoBehaviour
{
    List<string> commands;
    int currentIndex;
    /// <summary>
    /// Whether command parsed in is not recognized, set to false for all default and custom commands if recognized. stop: Whether a command is manually stopped.
    /// </summary>
    public bool error, stop;
    Text console;
    InputField field;
    /// <summary>
    /// List of help messages.
    /// </summary>
    public List<string> helpMessages;
    /// <summary>
    /// Custom delegates for custom commands.
    /// </summary>
    public List<Action<List<string>>> Commands;
    Raymarch.Render render;

    // Use this for initialization
    void Start()
    {
        commands = new List<string>();
        helpMessages = new List<string>();
        render = Raymarch.GetRenderer.GetRendererMethod();
        Commands = new List<Action<List<string>>>();
        console = GameObject.FindGameObjectWithTag("Console").GetComponent<Text>();
        field = gameObject.GetComponent<InputField>();

        Commands.Add(DefaultCommands);
        AddHelpMessages();
    }

    void AddHelpMessages()
    {
        helpMessages.Add("tp (3d vector): Teleport to location instantaneously.");
        helpMessages.Add("help (int: page): Open help page for page number n.");
        helpMessages.Add("travel (3d vec: target, float vel): Travel to target at velocity (multiple of lightspeed).");
        helpMessages.Add("stop: Stop a command.");
        helpMessages.Add("setFPS (float minFPS) (float maxFPS): Target FPS window for rendering.");
    }
    
    void Update()
    {
        render.builtinControls = !field.isFocused;
        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            currentIndex = Mathf.Clamp(currentIndex - 1, 0, commands.Count - 1);
            field.text = commands[currentIndex];
        }
        if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            currentIndex = Mathf.Clamp(currentIndex + 1, 0, commands.Count - 1);
            field.text = commands[currentIndex];
        }
    }
    public void DefaultCommands(List<string> keys)
    {
        if (keys[0] == "tp")
        {
            error = false;
            Vector3 tpPos = render.cameraPosition;
            if (keys.Count > 1 && keys[1] != "~") tpPos.x = float.Parse(keys[1]);
            if (keys.Count > 2 && keys[2] != "~") tpPos.y = float.Parse(keys[2]);
            if (keys.Count > 3 && keys[3] != "~") tpPos.z = float.Parse(keys[3]);

            render.cameraPosition = tpPos;

            LogConsole("Teleported to: " + tpPos.ToString());
        }

        if (keys[0] == "setFPS")
        {
            error = false;
            Vector2 tpPos = render.fpsBounds;
            if (keys.Count > 1 && keys[1] != "~") tpPos.x = float.Parse(keys[1]);
            if (keys.Count > 2 && keys[2] != "~") tpPos.y = float.Parse(keys[2]);

            render.fpsBounds = tpPos;

            LogConsole("Set Min/Max to: " + tpPos.x + ", " + tpPos.y + " respectively.");
        }

        if (keys[0] == "help")
        {
            error = false;
            console.text = "";

            int var = 0;
            if (keys.Count > 1) var = int.Parse(keys[1]);

            int page = 0;
            int chars = 0;
            for (int i = 0; i < helpMessages.Count; i++)
            {
                chars = (int)Math.Ceiling(chars / 23d + 0.001) * 23;

                if (page == var)
                {
                    LogConsole(helpMessages[i]);
                }

                for (int j = 0; j < helpMessages[i].Length; j++)
                {
                    if (helpMessages[i][j].ToString() == "\n")
                        chars = (int)Math.Ceiling(chars / 23d + 0.001) * 23;
                    else
                        chars++;
                }

                if (chars >= 320)
                {
                    chars = 0;
                    page++;
                    i--;
                    continue;
                }
            }
        }

        if (keys[0] == "travel")
        {
            error = false;
            Vector3 tpPos = render.cameraPosition;
            float speed = 1f;
            if (keys.Count > 1 && keys[1] != "~") tpPos.x = float.Parse(keys[1]);
            if (keys.Count > 2 && keys[2] != "~") tpPos.y = float.Parse(keys[2]);
            if (keys.Count > 3 && keys[3] != "~") tpPos.z = float.Parse(keys[3]);
            if (keys.Count > 4 && keys[4] != "~") speed = float.Parse(keys[4]);

            LogConsole("Travelling to: " + tpPos.ToString() + " at " + speed + "c");
            StartCoroutine(Travel(tpPos, speed, render));
        }

        if (keys[0] == "stop")
        {
            LogConsole("Stopped Command.");
            error = false;
            stop = true;
        }
    }

    IEnumerator Travel(Vector3 target, float speed, Raymarch.Render cam)
    {
        while ((cam.cameraPosition - target).magnitude > speed * cam.simSpeed * 1.4f && !stop)
        {
            yield return new WaitForEndOfFrame();
            Vector3 vel = (target - cam.cameraPosition).normalized * speed * cam.simSpeed * 1.4f;
            cam.cameraPosition += vel;
        }
    }

    /// <summary>
    /// Logs to visible console.
    /// </summary>
    /// <param name="str">string to print.</param>
    /// <param name="newline">Whether to append to new line or to current line.</param>
    public void LogConsole(string str, bool newline = true)
    {
        if (newline)
            console.text += "\n";

        console.text += str;

        int chars = 0;
        for (int i = 0; i < console.text.Length; i++)
        {
            if (console.text[i].ToString() == "\n")
                chars = (int)Math.Ceiling(chars / 23d + 0.001) * 23;
            else
                chars++;
        }
        if (chars > 322)
        {
            string final = "";
            int linebreak = 0;
            for (int i = 0; i < console.text.Length; i++)
            {
                if (console.text[i].ToString() == "\n")
                    linebreak++;

                if (linebreak > 1)
                    final += console.text[i].ToString();
            }
            console.text = final;
        }
    }

    List<string> Parser(string comm)
    {
        char[] chars = comm.ToCharArray();
        List<string> finalResult = new List<string>();
        string current = "";
        for (int i = 0; i < chars.Length; i++)
        {
            if (chars[i].ToString() != " ")
                current += chars[i].ToString();

            if (chars[i].ToString() == " " || i >= chars.Length - 1)
            {
                if (current.Length > 0)
                {
                    finalResult.Add(current);
                }
                current = "";
            }
        }
        return finalResult;
    }

    public void OnEnter()
    {
        if (field.text != "" && Input.GetKey(KeyCode.Return))
        {
            error = true;
            stop = false;
            List<string> res = Parser(field.text);
            commands.Add(field.text);
            currentIndex = commands.Count;

            foreach (Action<List<string>> command in Commands)
            {
                command.Invoke(res);
            }

            if (error)
                LogConsole("'" + res[0] + "' is not a recognized command.");
            field.text = "";
        }
    }
}
