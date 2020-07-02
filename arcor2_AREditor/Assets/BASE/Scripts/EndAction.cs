using System;
using System.Collections.Generic;

public class EndAction : StartEndAction
{
    private void Awake() {
        IO.Swagger.Model.Action projectAction = new IO.Swagger.Model.Action(
            flows: new List<IO.Swagger.Model.Flow>(),
            id: "END",
            name: "END",
            parameters: new List<IO.Swagger.Model.ActionParameter>(),
            type: "");

        Init(projectAction, null, null, null, "END");
    }
}
