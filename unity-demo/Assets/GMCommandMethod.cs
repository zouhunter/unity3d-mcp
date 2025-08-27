using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityMcp.Tools;
using Newtonsoft.Json.Linq;
using UnityMcp.Models;

[ToolName("gm_command")]
public class GMCommandMethod : StateMethodBase
{
    protected override StateTree CreateStateTree()
    {
        return StateTreeBuilder.Create()
            .Key("action")
            .DefaultLeaf(Execute)
            .Build();
    }

    private object Execute(JObject args)
    {
        return Response.Success("Test");
    }
}
