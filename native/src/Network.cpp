#include "Network.h"

#include <arpa/inet.h>
#include <csignal>
#include <dirent.h>
#include <ifaddrs.h>
#include <net/if.h>
#include <sys/wait.h>
#include <unistd.h>

#include <algorithm>
#include <cerrno>
#include <cstdio>
#include <cstring>
#include <fstream>
#include <string>
#include <vector>

#include <nlohmann/json.hpp>

#include "Errors.h"

namespace GHIElectronics::Endpoint::Internal::Network {

namespace {

constexpr const char* kConfigPath = "/config/network.json";

// Forward declarations so ApplyStatic can reference MetricForType, which
// (together with the gateway/route helpers) is defined later for grouping.
int MetricForType(Type type);

void RunAndWait(const char* path, char* const argv[]) {
    pid_t child = fork();
    if (child == 0) {
        execv(path, argv);
        _exit(127);
    }
    if (child > 0) {
        while (waitpid(child, nullptr, 0) < 0 && errno == EINTR) {}
    }
}

Type ClassifyInterface(const std::string& iface) {
    if (iface == "lo") {
        return Type::Ignore;
    }
    std::string base = "/sys/class/net/" + iface;
    if (access((base + "/wireless").c_str(), F_OK) == 0) {
        return Type::Wifi;
    }
    std::ifstream typefile(base + "/type");
    int kernelType = 0;
    typefile >> kernelType;
    return kernelType == 1 ? Type::Ethernet : Type::Ignore;  // 1 = ARPHRD_ETHER
}

std::string ReadMac(const std::string& iface) {
    std::ifstream f("/sys/class/net/" + iface + "/address");
    std::string mac;
    std::getline(f, mac);
    std::transform(mac.begin(), mac.end(), mac.begin(), ::tolower);
    return mac;
}

nlohmann::json ReadConfig() {
    try {
        std::ifstream file(kConfigPath);
        if (!file) {
            return nlohmann::json::object();
        }
        nlohmann::json config;
        file >> config;
        return config;
    } catch (...) {
        return nlohmann::json::object();
    }
}

// Null-safe JSON helpers: factory settings use `null` for "not set", and
// nlohmann/json's .value(key, default) throws when the key is null.
std::string JsonString(const nlohmann::json& j, const char* key) {
    auto it = j.find(key);
    return (it != j.end() && it->is_string()) ? it->get<std::string>() : std::string{};
}
bool JsonBool(const nlohmann::json& j, const char* key, bool fallback) {
    auto it = j.find(key);
    return (it != j.end() && it->is_boolean()) ? it->get<bool>() : fallback;
}
std::vector<std::string> JsonStringArray(const nlohmann::json& j, const char* key) {
    std::vector<std::string> out;
    auto it = j.find(key);
    if (it != j.end() && it->is_array()) {
        for (const auto& item : *it) {
            if (item.is_string()) {
                out.push_back(item.get<std::string>());
            }
        }
    }
    return out;
}
nlohmann::json JsonObject(const nlohmann::json& j, const char* key) {
    auto it = j.find(key);
    return (it != j.end() && it->is_object()) ? *it : nlohmann::json::object();
}

int MaskToPrefix(const std::string& mask) {
    if (mask.empty()) return -1;
    if (mask.find('.') == std::string::npos) {
        try {
            int p = std::stoi(mask);
            return (p >= 0 && p <= 32) ? p : -1;
        } catch (...) {
            return -1;
        }
    }
    in_addr addr{};
    if (inet_pton(AF_INET, mask.c_str(), &addr) != 1) return -1;
    uint32_t host = ntohl(addr.s_addr);
    int bits = 0;
    while (host & 0x80000000u) { host <<= 1; ++bits; }
    return host == 0 ? bits : -1;
}

void LinkSet(const std::string& iface, const char* state) {
    char iface_buf[IFNAMSIZ];
    std::snprintf(iface_buf, sizeof iface_buf, "%s", iface.c_str());
    char state_buf[8];
    std::snprintf(state_buf, sizeof state_buf, "%s", state);
    char* argv[] = {const_cast<char*>("ip"),
                    const_cast<char*>("link"),
                    const_cast<char*>("set"),
                    iface_buf,
                    state_buf,
                    nullptr};
    RunAndWait("/sbin/ip", argv);
}

void AddrFlush(const std::string& iface) {
    char iface_buf[IFNAMSIZ];
    std::snprintf(iface_buf, sizeof iface_buf, "%s", iface.c_str());
    char* argv[] = {const_cast<char*>("ip"),
                    const_cast<char*>("addr"),
                    const_cast<char*>("flush"),
                    const_cast<char*>("dev"),
                    iface_buf,
                    nullptr};
    RunAndWait("/sbin/ip", argv);
}

// Waits up to a few seconds for the interface to show carrier (ETHTOOL_GLINK
// semantics via sysfs). An ethernet PHY takes ~1-3 s to negotiate link after
// the interface comes administratively up. Returns true if carrier came up.
bool WaitForCarrier(const std::string& iface, int timeout_ms) {
    std::string path = "/sys/class/net/" + iface + "/carrier";
    for (int waited = 0; waited < timeout_ms; waited += 100) {
        std::ifstream f(path);
        int c = 0;
        f >> c;
        if (c == 1) return true;
        usleep(100 * 1000);
    }
    return false;
}

void StartDhcp(const std::string& iface) {
    // Give the PHY a moment to negotiate link. udhcpc fires DHCPDISCOVER
    // immediately on start - without carrier the broadcast goes nowhere and
    // udhcpc's retry window burns through before the link is actually up.
    WaitForCarrier(iface, 4000);

    char iface_buf[IFNAMSIZ];
    std::snprintf(iface_buf, sizeof iface_buf, "%s", iface.c_str());
    char pidfile[64];
    std::snprintf(pidfile, sizeof pidfile, "/var/run/udhcpc.%s.pid", iface.c_str());
    // -b: fork to background after writing pidfile (so Enable() returns promptly).
    // No -q: udhcpc stays running and keeps renewing the lease.
    // No -R: don't release the lease on exit; a lease that outlives the program
    //        is fine for an embedded device.
    char* argv[] = {const_cast<char*>("udhcpc"),
                    const_cast<char*>("-i"), iface_buf,
                    const_cast<char*>("-p"), pidfile,
                    const_cast<char*>("-b"),
                    nullptr};
    RunAndWait("/sbin/udhcpc", argv);
}

void StopDhcp(const std::string& iface) {
    std::string pidfile = "/var/run/udhcpc." + iface + ".pid";
    std::ifstream f(pidfile);
    int pid = 0;
    f >> pid;
    if (pid > 0) {
        kill(pid, SIGTERM);
    }
}

void ApplyStatic(const std::string& iface, Type type,
                 const nlohmann::json& static_cfg,
                 const std::vector<std::string>& dns) {
    std::string ip = JsonString(static_cfg, "ip");
    std::string mask = JsonString(static_cfg, "mask");
    std::string gateway = JsonString(static_cfg, "gateway");
    if (ip.empty() || mask.empty()) {
        throw EndpointError(ErrorKind::InvalidOperation,
                            "static ip/mask missing in /config/network.json");
    }
    int prefix = MaskToPrefix(mask);
    if (prefix < 0) {
        throw EndpointError(ErrorKind::InvalidOperation,
                            "bad mask '" + mask + "' in /config/network.json");
    }
    AddrFlush(iface);
    char iface_buf[IFNAMSIZ];
    std::snprintf(iface_buf, sizeof iface_buf, "%s", iface.c_str());
    char addr[64];
    std::snprintf(addr, sizeof addr, "%s/%d", ip.c_str(), prefix);
    {
        char* argv[] = {const_cast<char*>("ip"), const_cast<char*>("addr"),
                        const_cast<char*>("add"), addr,
                        const_cast<char*>("dev"), iface_buf, nullptr};
        RunAndWait("/sbin/ip", argv);
    }
    if (!gateway.empty()) {
        char gw[64];
        std::snprintf(gw, sizeof gw, "%s", gateway.c_str());
        char metric_buf[16];
        std::snprintf(metric_buf, sizeof metric_buf, "%d", MetricForType(type));
        char* argv[] = {const_cast<char*>("ip"), const_cast<char*>("route"),
                        const_cast<char*>("add"), const_cast<char*>("default"),
                        const_cast<char*>("via"), gw,
                        const_cast<char*>("dev"), iface_buf,
                        const_cast<char*>("metric"), metric_buf, nullptr};
        RunAndWait("/sbin/ip", argv);
    }
    if (!dns.empty()) {
        std::ofstream resolv("/etc/resolv.conf");
        for (const auto& server : dns) {
            resolv << "nameserver " << server << '\n';
        }
    }
}

void StartWpaSupplicant(const std::string& iface, const std::string& ssid,
                        const std::string& password, const std::string& security) {
    std::string conf_path = "/run/wpa_supplicant." + iface + ".conf";
    {
        std::ofstream conf(conf_path);
        conf << "ctrl_interface=/var/run/wpa_supplicant\n";
        conf << "network={\n  ssid=\"" << ssid << "\"\n";
        if (security == "none" || security == "open") {
            conf << "  key_mgmt=NONE\n";
        } else {
            conf << "  psk=\"" << password << "\"\n  key_mgmt=WPA-PSK\n";
        }
        conf << "}\n";
    }
    char iface_buf[IFNAMSIZ];
    std::snprintf(iface_buf, sizeof iface_buf, "%s", iface.c_str());
    {
        char* argv[] = {const_cast<char*>("wpa_cli"), const_cast<char*>("-i"),
                        iface_buf, const_cast<char*>("terminate"), nullptr};
        RunAndWait("/usr/sbin/wpa_cli", argv);
    }
    char pidfile[64];
    std::snprintf(pidfile, sizeof pidfile, "/var/run/wpa_supplicant.%s.pid", iface.c_str());
    char conf_buf[256];
    std::snprintf(conf_buf, sizeof conf_buf, "%s", conf_path.c_str());
    char* argv[] = {const_cast<char*>("wpa_supplicant"), const_cast<char*>("-B"),
                    const_cast<char*>("-i"), iface_buf,
                    const_cast<char*>("-c"), conf_buf,
                    const_cast<char*>("-P"), pidfile, nullptr};
    RunAndWait("/usr/sbin/wpa_supplicant", argv);
}

const char* SectionKey(Type type) {
    switch (type) {
        case Type::Ethernet: return "ethernet";
        case Type::Wifi: return "wifi";
        default: return "";
    }
}

// Default-route priority between kinds. Lowest metric wins when more than
// one interface has a default route (standard Linux routing semantics):
//   ethernet (100) < wifi (200)
// So when the user enables both ethernet and wifi, ethernet carries outbound
// traffic. Promise from [[feedback-no-auto-network]].
int MetricForType(Type type) {
    switch (type) {
        case Type::Ethernet: return 100;
        case Type::Wifi: return 200;
        default: return 1000;
    }
}

// Captures stdout of a short-running command. Trimmed trailing newline.
std::string RunAndCapture(const std::string& cmd) {
    FILE* f = popen(cmd.c_str(), "r");
    if (f == nullptr) return "";
    std::string out;
    char buf[256];
    while (fgets(buf, sizeof buf, f) != nullptr) {
        out += buf;
    }
    pclose(f);
    while (!out.empty() && (out.back() == '\n' || out.back() == '\r')) out.pop_back();
    return out;
}

// Returns the IPv4 default-route gateway address for the interface, or empty
// if no default route exists on this interface. Parses `ip route` output of
// the form "default via <gw> dev <iface> ..."
std::string GetDefaultGateway(const std::string& iface) {
    std::string out = RunAndCapture("/sbin/ip -4 route show default dev " + iface);
    // "default via 192.168.0.1 dev eth0 ..."
    auto via = out.find("via ");
    if (via == std::string::npos) return "";
    via += 4;  // skip "via "
    auto end = out.find(' ', via);
    return out.substr(via, (end == std::string::npos) ? std::string::npos : end - via);
}

// Waits for udhcpc to install a default route on the interface. Returns true
// if one appeared within the timeout.
bool WaitForDefaultRoute(const std::string& iface, int timeout_ms) {
    for (int waited = 0; waited < timeout_ms; waited += 200) {
        if (!GetDefaultGateway(iface).empty()) return true;
        usleep(200 * 1000);
    }
    return false;
}

// Deletes whatever default route is on the interface and re-adds it with the
// explicit metric for the kind. This is how we enforce "ethernet beats wifi":
// both can be up with internet, but ethernet's metric is lower and the kernel
// picks it for outbound traffic.
void SetDefaultRouteMetric(const std::string& iface, int metric) {
    std::string gw = GetDefaultGateway(iface);
    if (gw.empty()) return;

    char iface_buf[IFNAMSIZ];
    std::snprintf(iface_buf, sizeof iface_buf, "%s", iface.c_str());
    char gw_buf[64];
    std::snprintf(gw_buf, sizeof gw_buf, "%s", gw.c_str());
    char metric_buf[16];
    std::snprintf(metric_buf, sizeof metric_buf, "%d", metric);

    char* del_argv[] = {const_cast<char*>("ip"), const_cast<char*>("route"),
                        const_cast<char*>("del"), const_cast<char*>("default"),
                        const_cast<char*>("via"), gw_buf,
                        const_cast<char*>("dev"), iface_buf, nullptr};
    RunAndWait("/sbin/ip", del_argv);

    char* add_argv[] = {const_cast<char*>("ip"), const_cast<char*>("route"),
                        const_cast<char*>("add"), const_cast<char*>("default"),
                        const_cast<char*>("via"), gw_buf,
                        const_cast<char*>("dev"), iface_buf,
                        const_cast<char*>("metric"), metric_buf, nullptr};
    RunAndWait("/sbin/ip", add_argv);
}

}  // namespace

// Returns true if the interface currently has IFF_UP set (admin up). After
// a successful `ip link set <iface> up`, this flag is set. A dead netdev
// (e.g. STM32 ethernet whose PHY failed to attach) rejects "set up" and the
// flag stays clear, so we can detect "can this interface actually be
// enabled?" by trying and then checking.
bool IsAdminUp(const std::string& iface) {
    std::ifstream f("/sys/class/net/" + iface + "/flags");
    unsigned flags = 0;
    f >> std::hex >> flags;
    return (flags & 0x1u) != 0;  // IFF_UP
}

// Returns every interface of the given type, in alphabetical order of their
// system identifiers. Honours the MAC filter from /config/network.json if
// set: only interfaces with that MAC are returned.
std::vector<std::string> GetMatchingInterfaces(Type wanted) {
    std::string preferredMac;
    try {
        nlohmann::json config = ReadConfig();
        nlohmann::json section = JsonObject(config, SectionKey(wanted));
        preferredMac = JsonString(section, "mac");
        std::transform(preferredMac.begin(), preferredMac.end(),
                       preferredMac.begin(), ::tolower);
    } catch (...) {
    }

    std::vector<std::string> all;
    DIR* dir = opendir("/sys/class/net");
    if (dir == nullptr) return all;
    while (dirent* entry = readdir(dir)) {
        std::string name = entry->d_name;
        if (name == "." || name == "..") continue;
        if (ClassifyInterface(name) != wanted) continue;
        if (!preferredMac.empty() && ReadMac(name) != preferredMac) continue;
        all.push_back(name);
    }
    closedir(dir);
    std::sort(all.begin(), all.end());
    return all;
}

// True if the interface has a Layer-1 carrier (cable plugged in, link up,
// WiFi associated). Reads /sys/class/net/<iface>/carrier.
bool HasCarrier(const std::string& iface) {
    std::ifstream f("/sys/class/net/" + iface + "/carrier");
    int c = 0;
    f >> c;
    return c == 1;
}

std::string GetInterfaceName(Type wanted) {
    // Preference order, matching Enable()'s own candidate pick:
    //   1. admin-up AND has carrier (cable plugged in / associated)
    //   2. admin-up (Enable() succeeded here, but cable not plugged)
    //   3. first alphabetical (no interface has been enabled yet)
    // On a board with built-in Ethernet (eth0) and a USB-Ethernet dongle
    // (eth2) where only the dongle has a cable, Enable() picks eth2 for
    // DHCP; Name / IsConnected / IsEnabled must agree, so this function
    // must return eth2 too - not eth0 just because it is alphabetically
    // first and admin-up.
    auto candidates = GetMatchingInterfaces(wanted);
    for (const auto& iface : candidates) {
        if (IsAdminUp(iface) && HasCarrier(iface)) {
            return iface;
        }
    }
    for (const auto& iface : candidates) {
        if (IsAdminUp(iface)) {
            return iface;
        }
    }
    return candidates.empty() ? std::string{} : candidates.front();
}

void Enable(Type type) {
    // Load the kernel driver(s) for USB-Ethernet dongles on demand. The
    // built-in MA35D1 Ethernet uses nuvoton-dwmac, compiled =y, so eth0 /
    // eth1 are present from boot without modprobe. The USB-Ethernet drivers
    // ship as modules so boards that never plug in an adapter pay zero
    // boot cost; the first Enable() modprobes every common chip family.
    // Each modprobe is independent - a module not matching the customer's
    // adapter just no-ops (the modprobe either succeeds and the driver sits
    // idle, or fails silently with "module not found" and we move on).
    const char* const* modList = nullptr;
    size_t modCount = 0;
    static const char* const ethMods[] = {
        "usbnet",            // shared USB-NET core
        "r8152",             // Realtek RTL8152 / RTL8153
        "ax88179_178a",      // ASIX AX88179 / AX88178A (gigabit)
        "asix",              // ASIX AX8817x (10/100)
        "smsc95xx",          // SMSC9500 series
        "cdc_ether",         // CDC Ethernet class (standard)
        "cdc_ncm",           // CDC NCM class (standard)
    };
    static const char* const wifiMods[] = {
        "cfg80211",          // Wi-Fi config layer (pulled by MAC drivers)
        "mac80211",          // soft-MAC layer (pulled by driver below)
        "rtl8xxxu",          // Realtek 8188CUS/8188CU/8192CU/8723AU
        "rtl8192cu",         // older rtlwifi driver for same chip family
        "ath9k_htc",         // Atheros AR9170/AR9271 USB
        "mt76x2u",           // MediaTek MT7612U
        "brcmfmac",          // Broadcom BCM43xx (incl. BCM43143 USB)
    };
    if (type == Type::Ethernet) {
        modList = ethMods;
        modCount = sizeof(ethMods) / sizeof(ethMods[0]);
    } else if (type == Type::Wifi) {
        modList = wifiMods;
        modCount = sizeof(wifiMods) / sizeof(wifiMods[0]);
    }
    for (size_t i = 0; i < modCount; ++i) {
        char* argv[] = {const_cast<char*>("modprobe"),
                        const_cast<char*>("-q"),
                        const_cast<char*>(modList[i]), nullptr};
        RunAndWait("/sbin/modprobe", argv);
    }
    // (WiFi driver loading is handled by wpa_supplicant / udhcpc paths
    // further down; USB WiFi dongles bring their own drivers.)

    // Candidates for this type, honouring the MAC pin from network.json
    // if set. On a board with a dead built-in PHY and a working USB
    // ethernet adapter plugged in, both netdevs exist; we try each in
    // alphabetical order and use the first one `ip link set up` succeeds
    // on, so the user does not need to pin a MAC just to work around a
    // non-functional netdev.
    auto candidates = GetMatchingInterfaces(type);
    if (candidates.empty()) {
        throw EndpointError(ErrorKind::InvalidOperation,
                            std::string("no ") + SectionKey(type) +
                            " interface is currently present");
    }

    nlohmann::json config = ReadConfig();
    nlohmann::json section = JsonObject(config, SectionKey(type));

    // For WiFi, wpa_supplicant must be started per interface. Needs ssid.
    std::string ssid, password, security;
    if (type == Type::Wifi) {
        ssid = JsonString(section, "ssid");
        if (ssid.empty()) {
            throw EndpointError(ErrorKind::InvalidOperation,
                                "wifi ssid is empty in /config/network.json");
        }
        password = JsonString(section, "password");
        security = JsonString(section, "security");
        if (security.empty()) security = "wpa2";
    }

    // Bring every candidate admin-up; collect the ones that accepted it
    // (a dead PHY would reject). Then pick the one with carrier (cable
    // plugged in); fall back to the first admin-up if none has carrier,
    // so a cable-less DHCP attempt still produces a clear error.
    std::vector<std::string> adminUp;
    for (const auto& candidate : candidates) {
        if (type == Type::Wifi) {
            StartWpaSupplicant(candidate, ssid, password, security);
        }
        LinkSet(candidate, "up");
        usleep(100 * 1000);  // let the kernel update flags
        if (IsAdminUp(candidate)) {
            adminUp.push_back(candidate);
        }
    }
    if (adminUp.empty()) {
        throw EndpointError(ErrorKind::InvalidOperation,
                            std::string("no ") + SectionKey(type) +
                            " interface could be brought up");
    }
    std::string iface;
    for (const auto& candidate : adminUp) {
        if (WaitForCarrier(candidate, 1000)) {
            iface = candidate;
            break;
        }
    }
    if (iface.empty()) {
        iface = adminUp.front();
    }

    int metric = MetricForType(type);
    if (JsonBool(section, "dhcp", true)) {
        StartDhcp(iface);
        // udhcpc installs a default route with its own metric (0). Wait for it
        // to appear, then re-add with our explicit metric so that if a second
        // interface comes up later (e.g. wifi after ethernet), the kernel
        // prefers the one with the lower metric. See [[feedback-no-auto-network]].
        if (WaitForDefaultRoute(iface, 10000)) {
            SetDefaultRouteMetric(iface, metric);
        }
    } else {
        auto static_cfg = JsonObject(section, "static");
        auto dns = JsonStringArray(static_cfg, "dns");
        ApplyStatic(iface, type, static_cfg, dns);
    }
}

void Disable(Type type) {
    std::string iface = GetInterfaceName(type);
    if (iface.empty()) return;
    StopDhcp(iface);
    LinkSet(iface, "down");
}

bool IsEnabled(Type type) {
    std::string iface = GetInterfaceName(type);
    if (iface.empty()) return false;
    std::ifstream f("/sys/class/net/" + iface + "/flags");
    unsigned flags = 0;
    f >> std::hex >> flags;
    return (flags & 0x1u) != 0;  // IFF_UP
}

bool IsConnected(Type type) {
    std::string iface = GetInterfaceName(type);
    if (iface.empty()) return false;

    // Physical link must be up (cable plugged for ethernet, associated for
    // WiFi). Without this, a stale DHCP lease would make us report
    // "connected" after the cable is pulled.
    std::ifstream cf("/sys/class/net/" + iface + "/carrier");
    int carrier = 0;
    cf >> carrier;
    if (carrier != 1) return false;

    // And must have an IPv4 address.
    ifaddrs* list = nullptr;
    if (getifaddrs(&list) != 0) return false;
    bool found = false;
    for (auto* a = list; a != nullptr; a = a->ifa_next) {
        if (a->ifa_addr != nullptr
                && a->ifa_addr->sa_family == AF_INET
                && a->ifa_name != nullptr
                && iface == a->ifa_name) {
            found = true;
            break;
        }
    }
    freeifaddrs(list);
    return found;
}

}  // namespace GHIElectronics::Endpoint::Internal::Network
