#!/bin/bash
# =============================================================================
# Custom MongoDB Entrypoint for Replica Set Support
# =============================================================================
#
# PURPOSE:
#   This script fixes a file permission issue that occurs when mounting the
#   MongoDB keyfile from Windows/Mac hosts into the Docker container.
#
# WHY IS THIS NEEDED?
#   1. MongoDB requires a keyfile for replica set authentication
#   2. The keyfile MUST have permissions 400 (owner read-only)
#   3. When Docker mounts a file from Windows, it gets permissions 755
#   4. You cannot chmod a mounted file (it's read-only from the host)
#   5. MongoDB refuses to start if keyfile permissions are too open
#
# SOLUTION:
#   Copy the keyfile to /tmp (writable), set correct permissions, then start MongoDB
#
# WHY REPLICA SET?
#   MongoDB transactions require a replica set - even a single-node one.
#   Without it, the Unit of Work pattern and ACID transactions won't work.
#
# =============================================================================

# Copy keyfile from mounted location to a writable location
cp /etc/mongodb-keyfile-source /tmp/mongodb-keyfile

# Set permissions to 400 (owner read-only) - required by MongoDB
chmod 400 /tmp/mongodb-keyfile

# Change ownership to mongodb user
chown mongodb:mongodb /tmp/mongodb-keyfile

# Execute the original MongoDB entrypoint with all passed arguments
# The --keyFile flag in docker-compose.yml points to /tmp/mongodb-keyfile
exec docker-entrypoint.sh "$@"
