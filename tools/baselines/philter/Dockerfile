# Philter (philter-ucsf 1.0.3, BSD 3-Clause) in its default configuration, as a baseline for
# the evaluation tool. Its regexes need Python 3.10: later versions refuse their inline flags.
# Every package is pinned by hash (requirements.txt, from requirements.in with pip-compile
# --generate-hashes on this image), and NLTK's POS tagger, the only model it uses, by checksum.
#
#   docker build -t deidsharp/philter:1.0.3 tools/baselines/philter
#   docker run --rm --network none -v <export>/text:/data/in:ro -v <output>:/data/out deidsharp/philter:1.0.3
#
# The container needs no network to run; the notes never leave the machine.
FROM python:3.10-slim@sha256:6ff506466f8b1e981719b468cc0023b8ef000b983cdec0eafe119d01f379e31d

COPY requirements.txt /tmp/requirements.txt
RUN pip install --no-cache-dir --require-hashes --requirement /tmp/requirements.txt

# NLTK's own downloader fetches the latest upload; the checksum holds it to the one measured.
ADD --checksum=sha256:6025f530624335c67d6547d44757b357b4e79bae030a0383e9887a92c1718f0b \
    https://raw.githubusercontent.com/nltk/nltk_data/gh-pages/packages/taggers/averaged_perceptron_tagger_eng.zip \
    /usr/share/nltk_data/taggers/averaged_perceptron_tagger_eng.zip
RUN python -c "import zipfile; zipfile.ZipFile('/usr/share/nltk_data/taggers/averaged_perceptron_tagger_eng.zip').extractall('/usr/share/nltk_data/taggers')"

COPY run-philter.py /opt/run-philter.py
USER nobody
ENTRYPOINT ["python", "-O", "/opt/run-philter.py", "/data/in", "/data/out"]
