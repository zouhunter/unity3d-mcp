using System;
using System.Collections.Generic; // Added for HashSet
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityMcp.Helpers; // For Response class
using UnityMcp.Tools; // 添加这个引用
using UnityMcp;

namespace UnityMcp.Tools
{
    /// <summary>
    /// Handles executing Unity Editor menu items by path.
    /// 对应方法名: execute_menu_item
    /// </summary>
    public class ExecuteMenuItem : IToolMethod
    {
        // Basic blacklist to prevent accidental execution of potentially disruptive menu items.
        // This can be expanded based on needs.
        private static readonly HashSet<string> _menuPathBlacklist = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase
        )
        {
            "File/Quit",
            // Add other potentially dangerous items like "Edit/Preferences...", "File/Build Settings..." if needed
        };

        // 实现IToolMethod接口 —— 使用 StateTree 分发
        public object ExecuteMethod(JObject args)
        {
            string action = args["action"]?.ToString()?.ToLower() ?? "execute";
            var stateTree = StateTreeBuilder.Create()
                .Key("action")
                    .Leaf("execute", ExecuteItem)
                    .Leaf("get_available_menus", GetAvailableMenus)
                    .DefaultLeaf((ctx) => Response.Error($"Unknown action: '{action}' for execute_menu_item"))
                .Build();

            return stateTree.Run(args) ?? Response.Error("No action executed.");
        }

        /// <summary>
        /// Executes a specific menu item.
        /// </summary>
        private object ExecuteItem(JObject cmd)
        {
            string menuPath = cmd["menu_path"]?.ToString();
            // string alias =cmd["alias"]?.ToString(); // TODO: Implement alias mapping based on refactor plan requirements.
            // JObject args =cmd["args"] as JObject; // TODO: Investigate parameter passing (often not directly supported by ExecuteMenuItem).

            if (string.IsNullOrWhiteSpace(menuPath))
            {
                return Response.Error("Required parameter 'menu_path' is missing or empty.");
            }

            // Validate against blacklist
            if (_menuPathBlacklist.Contains(menuPath))
            {
                return Response.Error(
                    $"Execution of menu item '{menuPath}' is blocked for safety reasons."
                );
            }

            // TODO: Implement alias lookup here if needed (Map alias to actual menuPath).
            // if (!string.IsNullOrEmpty(alias)) { menuPath = LookupAlias(alias); if(menuPath == null) return Response.Error(...); }

            // TODO: Handle args ('args' object) if a viable method is found.
            // This is complex as EditorApplication.ExecuteMenuItem doesn't take arguments directly.
            // It might require finding the underlying EditorWindow or command if args are needed.

            try
            {
                // Attempt to execute the menu item on the main thread using delayCall for safety.
                EditorApplication.delayCall += () =>
                {
                    try
                    {
                        bool executed = EditorApplication.ExecuteMenuItem(menuPath);
                        // Log potential failure inside the delayed call.
                        if (!executed)
                        {
                            if (UnityMcp.EnableLog) Debug.LogError(
                                $"[ExecuteMenuItem] Failed to find or execute menu item via delayCall: '{menuPath}'. It might be invalid, disabled, or context-dependent."
                            );
                        }
                    }
                    catch (Exception delayEx)
                    {
                        if (UnityMcp.EnableLog) Debug.LogError(
                            $"[ExecuteMenuItem] Exception during delayed execution of '{menuPath}': {delayEx}"
                        );
                    }
                };

                // Report attempt immediately, as execution is delayed.
                return Response.Success(
                    $"Attempted to execute menu item: '{menuPath}'. Check Unity logs for confirmation or errors."
                );
            }
            catch (Exception e)
            {
                // Catch errors during setup phase.
                if (UnityMcp.EnableLog) Debug.LogError(
                    $"[ExecuteMenuItem] Failed to setup execution for '{menuPath}': {e}"
                );
                return Response.Error(
                    $"Error setting up execution for menu item '{menuPath}': {e.Message}"
                );
            }
        }

        private object GetAvailableMenus(JObject cmd)
        {
            // Getting a comprehensive list of *all* menu items dynamically is very difficult
            // and often requires complex reflection or maintaining a manual list.
            // Returning a placeholder/acknowledgement for now.
            if (UnityMcp.EnableLog) Debug.LogWarning(
                "[ExecuteMenuItem] 'get_available_menus' action is not fully implemented. Dynamically listing all menu items is complex."
            );
            // Returning an empty list as per the refactor plan's requirements.
            return Response.Success(
                "'get_available_menus' action is not fully implemented. Returning empty list.",
                new List<string>()
            );
        }

        // TODO: Add helper for alias lookup if implementing aliases.
        // private static string LookupAlias(string alias) { ... return actualMenuPath or null ... }
    }
}

