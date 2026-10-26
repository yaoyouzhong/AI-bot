/* Compile against the unchanged firmware rpc_mailbox.inc, using public data. */
#include <stdint.h>
#include <stdbool.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "rpc_mailbox.inc"

static void require(bool ok, const char *what) { if (!ok) { fprintf(stderr, "%s\n", what); exit(1); } }
static unsigned number(FILE *f) { uint8_t bytes[4]; require(fread(bytes, 1, 4, f) == 4, "fixture truncated"); return rpc_u32(bytes); }
int main(int argc, char **argv) {
    require(argc == 2, "binary fixture required");
    FILE *f = fopen(argv[1], "rb"); require(f != NULL, "fixture unavailable");
    unsigned total = number(f), response_total = number(f);
    require(total >= 124 && total <= 65535 && response_total >= 28 && response_total <= 65535, "invalid fixture size");
    uint8_t *request = malloc(total), *response = malloc(response_total), *received = calloc(65535, 1);
    require(request && response && received, "allocation failed");
    require(fread(request, 1, total, f) == total && fread(response, 1, response_total, f) == response_total, "fixture truncated");
    rpc_mailbox box = {.out=request, .in=received, .active=0x12345678, .size=total};
    unsigned fragments = number(f);
    for (unsigned i = 0; i < fragments; i++) {
        unsigned length = number(f); uint8_t expected[2056], actual[2056];
        require(length <= sizeof(expected) && fread(expected, 1, length, f) == length, "request fragment truncated");
        require(rpc_read(&box, actual, sizeof(actual)) == length && !memcmp(expected, actual, length), "request wire mismatch");
        if (box.offered < box.size) {
            uint8_t ack[7] = {1}; rpc_put32(ack+1, box.active); rpc_put16(ack+5, box.offered+1);
            require(!rpc_write(&box, ack, sizeof(ack)), "out-of-order ACK accepted");
            rpc_put16(ack+5, box.offered); require(rpc_write(&box, ack, sizeof(ack)), "valid ACK rejected");
        }
    }
    unsigned replies = number(f);
    for (unsigned i = 0; i < replies; i++) {
        unsigned length = number(f); uint8_t fragment[2057];
        require(length <= sizeof(fragment) && fread(fragment, 1, length, f) == length, "response fragment truncated");
        fragment[1] ^= 1; require(!rpc_write(&box, fragment, length), "wrong request identity accepted"); fragment[1] ^= 1;
        fragment[5] ^= 1; require(!rpc_write(&box, fragment, length), "response gap accepted"); fragment[5] ^= 1;
        require(rpc_write(&box, fragment, length), "valid response rejected");
    }
    require(box.complete && box.received == response_total && !memcmp(box.in, response, response_total), "response mismatch");
    require(fgetc(f) == EOF, "unexpected fixture tail"); fclose(f);
    free(request); free(response); free(received);
    printf("TAB5_FIRMWARE_MAILBOX_OK requestFragments=%u responseFragments=%u\n", fragments, replies);
    return 0;
}
