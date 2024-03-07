import os, mmap

MAP_MASK = mmap.PAGESIZE - 1

def Read(address):
    f = os.open("/dev/mem", os.O_RDWR | os.O_SYNC)

    m = mmap.mmap(f, mmap.PAGESIZE, mmap.MAP_SHARED, mmap.PROT_WRITE | mmap.PROT_READ, offset = address & ~MAP_MASK)
    m.seek(address & MAP_MASK)
    c = m.read(4)

    m.close()
    os.close(f)

    val = int.from_bytes(c, "little") 
    return val

def Write(address, value):
    f = os.open("/dev/mem", os.O_RDWR | os.O_SYNC)

    m = mmap.mmap(f, mmap.PAGESIZE, mmap.MAP_SHARED, mmap.PROT_WRITE | mmap.PROT_READ, offset = address & ~MAP_MASK)
    m.seek(address & MAP_MASK)

    a = value.to_bytes(4, "little")

    m.write(a)

    m.close()
    os.close(f)

    return

def SetBits(address, bits):
    val = Read(address)

    val |= bits

    Write(address, val)

    return

def ClearBits(address, bits):
    val = Read(address)

    val &= ~bits

    Write(address, val)




