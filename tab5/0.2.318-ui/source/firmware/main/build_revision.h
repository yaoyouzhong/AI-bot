// Internal candidates keep the public/protocol version unchanged.
// This marker also binds the package's revision metadata to the image bytes.
static const char tab5_build_revision[]="AIBOT_INTERNAL_REVISION=R13";
#define TAB5_INTERNAL_REVISION (tab5_build_revision+sizeof("AIBOT_INTERNAL_REVISION=")-1)
