using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityMcp.Helpers;
using UnityMcp;

namespace UnityMcp.Tools
{
    /// <summary>
    /// Handles executing Unity Editor menu items by path.
    /// 对应方法名: execute_menu_item
    /// </summary>
    public class ExecuteMenuItem : StateMethodBase
    {
        protected override StateTree CreateStateTree()
        {
            return StateTreeBuilder.Create()
                .Key("action")
                    .Leaf("execute", ExecuteItem)
                    .DefaultLeaf((ctx) => Response.Error($"Unknown action: '{ctx["action"]?.ToString() ?? "null"}' for execute_menu_item"))
                .Build();
        }

        /// <summary>
        /// Executes a specific menu item.
        /// </summary>
        private object ExecuteItem(JObject cmd)
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