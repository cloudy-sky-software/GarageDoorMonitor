import * as pulumi from "@pulumi/pulumi";

import { spawnSync } from "child_process";

/**
 * Calls `dotnet publish` at the specified path.
 */
export function buildFunctionsProject(path: string) {
    // Build the dotnet core project.
    const result = spawnSync("dotnet", ["publish", path]);

    pulumi.log.info(`stdout: ${result.stdout || result.stderr}`, undefined, 0, true);
}
