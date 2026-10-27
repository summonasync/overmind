using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Overmind.Mcp.ToolBinding;

public static class ToolBindingErrorFilter
{
    public static McpRequestFilter<CallToolRequestParams, CallToolResult> Create() =>
        next => async (request, cancellationToken) =>
        {
            if (request.MatchedPrimitive is McpServerTool tool)
            {
                var contract = ToolContractParser.Parse(tool.ProtocolTool.InputSchema);
                var violation = ToolBindingValidator.Find(request.Params?.Arguments, contract);
                if (violation is not null)
                {
                    return new CallToolResult
                    {
                        IsError = true,
                        Content =
                        [
                            new TextContentBlock
                            {
                                Text = ToolBindingErrorText.Render(tool.ProtocolTool.Name, violation, contract)
                            }
                        ]
                    };
                }
            }

            return await next(request, cancellationToken);
        };
}
