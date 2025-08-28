using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityMcp.Models;

namespace UnityMcp.Tools
{
    public class MenuUtils
    {
        /// <summary>
        /// Executes a specific menu item.
        /// </summary>
        public static object HandleExecuteMenu(JObject cmd)
        {
            string menuPath = cmd["menu_path"]?.ToString();

            if (string.IsNullOrWhiteSpace(menuPath))
            {
                return Response.Error("Required parameter 'menu_path' is missing or empty.");
            }

            bool executed = EditorApplication.ExecuteMenuItem(menuPath);
            if (!executed)
            {
                return Response.Error(
                    $"[ExecuteMenuItem] Failed to find or execute menu item via delayCall: '{menuPath}'. It might be invalid, disabled, or context-dependent."
                );
            }
            return Response.Success(
                $"Attempted to execute menu item: '{menuPath}'. Check Unity logs for confirmation or errors."
            );
        }
    }
}
