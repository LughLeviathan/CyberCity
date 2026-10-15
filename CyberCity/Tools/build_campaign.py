"""Rebuild the offline scenario pack and its corruption-detection checksum."""
import hashlib
import json
from pathlib import Path

def q(prompt, options, answer, explanation):
    return dict(prompt=prompt, options=options, answer=answer, explanation=explanation)

def e(label, text, suspicious, explanation):
    return dict(label=label, text=text, suspicious=suspicious, explanation=explanation)

def m(id, title, npc, area, kind, briefing, objective, takeaway, questions, **extra):
    return dict(id=id, title=title, npc=npc, area=area, kind=kind, briefing=briefing,
                objective=objective, takeaway=takeaway, questions=questions, **extra)

missions = [
    m('orientation', 'Welcome to CyberCity', 'Professor Byte', 'Cyber Academy', 'quiz',
      'Welcome, citizen. Every connection is a choice. Our city needs people who pause, verify, and help others do the same. Your patrol begins with a simple habit.',
      'Learn the city controls and choose the first response to an unexpected request.',
      'Pause before acting on an unexpected request. Verify it through an official channel you already trust. The Internet Cafe is now open.', [
        q('A message says your student account will close in ten minutes. What is your first move?',
          ['Use the message link before time runs out.', 'Open the official portal using your saved bookmark.', 'Send the sender your password to confirm your identity.'], 1,
          'Urgency is a pressure tactic. Verify through a channel you choose independently.'),
        q('How do you begin a mission with a mentor in CyberCity?',
          ['Approach the mentor and press E.', 'Click a real website in your browser.', 'Send a message to another player.'], 0,
          'Use WASD or arrow keys to move, then press E near a mentor. J opens your journal; M opens the map.')]),
    m('awareness', 'Small habits, strong defenses', 'Alex', 'Internet Cafe', 'quiz',
      'I used to reuse one password everywhere. Then one leaked account put all my others at risk. Help me build a better routine before we investigate the cafe inbox.',
      'Choose safer habits for passwords, verification codes, and software updates.',
      'Use a unique password for each account, enable MFA, keep software updated, and never share one-time verification codes.', [
        q('Which password habit limits damage if one service is breached?',
          ['Reuse a long password on every service.', 'Use your birth date with a symbol.', 'Use unique passwords stored in a password manager.'], 2,
          'Unique passwords prevent one stolen password from opening multiple accounts.'),
        q('Someone claiming to be support asks for your one-time sign-in code. What should you do?',
          ['Send it if they know your name.', 'Refuse, end the conversation, and contact official support.', 'Post the code in the group chat.'], 1,
          'A verification code can approve account access. Do not share it, even with someone who claims to be support.'),
        q('Why install security updates from the official software source?',
          ['They fix known weaknesses that attackers may exploit.', 'They make every attachment safe.', 'They replace the need for backups.'], 0,
          'Updates repair known flaws. Backups, cautious behavior, and account protection still matter.')]),
    m('phishing', 'The urgent inbox', 'Maya', 'Internet Cafe', 'evidence',
      'This message arrived in our student inbox. It looks official at first glance, but something feels wrong. Inspect the sender, request, and destination before deciding what to do.',
      'Highlight all three suspicious indicators in the message and choose a safe response.',
      'Check the actual sender and destination. Treat urgency and password requests as warning signs. Report the message, then verify through an official channel.', [
        q('The evidence points to phishing. What should Maya do next?',
          ['Reply with her password to ask whether it is real.', 'Open the link to inspect the sign-in form.', 'Report the message and verify through her saved school portal.'], 2,
          'Reporting helps others. Independent verification avoids trusting the same suspicious message.')],
      sender='Student Desk <support@campus-verify.example>', subject='ACTION REQUIRED: account suspension',
      document='Hello student, your access will be suspended. Review the details below.', address='', evidence=[
        e('01 / SENDER', 'support@campus-verify.example', True, 'LOOKALIKE SENDER\n\nThe cafe knows the official school domain is academy.example. This different domain needs independent verification. A display name proves nothing.'),
        e('02 / DEADLINE', 'You have 10 minutes to act.', True, 'PRESSURE TACTIC\n\nA very short deadline pushes you to skip checks. Slow down and verify through the official portal.'),
        e('03 / REQUEST', 'Enter your password at verify-campus.example', True, 'CREDENTIAL REQUEST\n\nThe message directs you to a different domain to enter a password. Use a known bookmark instead.'),
        e('04 / GREETING', 'Hello student', False, 'CONTEXT, NOT PROOF\n\nA generic greeting alone cannot decide whether a message is malicious. Judge the sender, destination, and request together.')]),
    m('fake-site', 'A convincing imitation', 'Detective Byte', 'Internet Cafe', 'evidence',
      'The cafe received a link to a scholarship page. The logo looks familiar. Investigate the address and the form. The real scholarship service is scholarships.academy.example.',
      'Inspect the fictional browser, identify three suspicious indicators, and classify the page.',
      'Read the full domain, not just the logo. HTTPS protects a connection but does not prove the site is honest. A scholarship should not ask for your email password. The Linux Lab is now open.', [
        q('Which conclusion is supported by the evidence?',
          ['This is a fake scholarship page. Leave it and report the URL.', 'The lock symbol proves the organization is trustworthy.', 'A familiar logo makes the password request reasonable.'], 0,
          'The mismatched domain and unnecessary password request outweigh the familiar branding.')],
      sender='', subject='Academy Scholarship Portal', address='https://academy-scholarships.example/claim',
      document='Congratulations! Your training scholarship is ready to claim.', evidence=[
        e('01 / DOMAIN', 'academy-scholarships.example', True, 'WRONG DOMAIN\n\nThe trusted service is scholarships.academy.example. This is a separate lookalike domain, not a subdomain of academy.example.'),
        e('02 / FORM', 'Enter your email account password.', True, 'EXCESSIVE DATA REQUEST\n\nA scholarship claim should not need the password to your email account. Never submit it here.'),
        e('03 / PROMISE', 'Guaranteed award. Claim in 5 minutes.', True, 'REWARD AND URGENCY\n\nAn unexpected guaranteed reward plus an artificial deadline is a reason to pause and verify.'),
        e('04 / TRANSPORT', 'HTTPS connection', False, 'ENCRYPTED DOES NOT MEAN HONEST\n\nHTTPS encrypts traffic to this domain. A deceptive site can also use HTTPS. Verify who owns the destination.')]),
    m('linux', 'A flag in the filesystem', 'SysAdmin Tux', 'Linux Lab', 'terminal',
      'Welcome to the command line. You do not need to memorize everything. Investigate one folder at a time. The hidden archive contains a flag that proves you read the evidence.',
      'Use pwd, ls -a, cd, and cat to find the hidden flag. Submit it in the terminal.',
      'The terminal is a way to inspect files deliberately. pwd shows location, ls -a includes hidden entries, cd changes folder, and cat reads evidence. The Web Security Lab is now open.', [
        q('What does ls -a add to a normal directory listing?',
          ['It downloads every file.', 'It includes hidden entries whose names start with a dot.', 'It gives administrator rights.'], 1,
          'The -a option includes hidden entries. It does not change file permissions or execute files.')]),
    m('idor', 'Who owns this record?', 'Red', 'Web Security Lab', 'idor',
      'A training API checks that you are signed in, but forgets to check whose record you requested. Observe the flaw in our fictional profile service, then switch to defense.',
      'Request training profile 1002 while signed in as Alex (1001), then select a server-side defense.',
      'IDOR is an authorization failure. Every server request must verify that the current user is allowed to access the requested object. Hiding IDs in the interface is not sufficient.', [
        q('What removes the cause of this IDOR flaw?',
          ['Hide the profile ID in the page.', 'Rename the API endpoint.', 'Verify record ownership or an allowed role on the server for every request.'], 2,
          'Server-side object authorization must run for each request. A signed-in user is not entitled to every object.')]),
    m('xss', 'When text becomes markup', 'Red', 'Web Security Lab', 'xss',
      'An unsafe comment page can confuse user text with page markup. Use a harmless formatting marker to model the problem. The training preview is inert and runs no scripts.',
      'Submit a harmless markup marker and choose the defense for untrusted output.',
      'Treat user content as data. Encode output for its destination context, use safe rendering APIs, and sanitize deliberately allowed markup with a maintained allowlist.', [
        q('Which defense addresses untrusted comments rendered into a page?',
          ['Encode output for the context and use safe text rendering.', 'Block only the word script.', 'Trust comments from signed-in users.'], 0,
          'Context-aware output encoding and safe rendering prevent text from being interpreted as code or markup.')]),
    m('sqli', 'Keep data out of the query', 'Red', 'Web Security Lab', 'sqli',
      'This fictional sign-in builds a database query by joining strings. Observe how a training input could alter its structure. No database is connected to this exercise.',
      'Inspect the simulated query and choose a defense that separates commands from values.',
      'Use parameterized queries with bound values. This keeps user data separate from SQL structure. Input validation and limited database permissions add further protection.', [
        q('What is the primary defense against this SQL injection flaw?',
          ['Remove spaces from every input.', 'Use parameterized queries and bind input as values.', 'Use a hidden login form.'], 1,
          'Bound parameters keep values separate from query syntax. Filters and hidden interfaces do not provide that separation.')]),
    m('red-team', 'Connect the evidence', 'Red', 'Web Security Lab', 'quiz',
      'You have seen three weaknesses in isolation. Now explain the pattern from evidence, without guessing or touching external systems. Each finding must lead to a concrete defense.',
      'Match three observations to IDOR, XSS, and SQL injection.',
      'A useful finding connects the trigger, unauthorized effect, and root cause. Test only within authorized scope, then pair every finding with a defense. Blue is ready to help.', [
        q('Alex changes a training record number and receives Mira\'s private record. What failed?',
          ['The color of the login button.', 'Server-side object authorization (IDOR).', 'The image compression setting.'], 1,
          'A different object ID should not bypass the ownership check on the server.'),
        q('A comment is interpreted as page markup instead of being displayed as text. What class of risk does this model?',
          ['Cross-site scripting (XSS).', 'A weak Wi-Fi signal.', 'A missing backup.'], 0,
          'Untrusted text entering an executable rendering context can lead to XSS. Use safe rendering and context-aware encoding.'),
        q('User input changes the meaning of a dynamically joined database query. What is the flaw?',
          ['IDOR.', 'Phishing.', 'SQL injection.'], 2,
          'The query mixed data with command syntax. Parameterized queries separate them.')]),
    m('blue-team', 'Repair the trust boundaries', 'Blue', 'Web Security Lab', 'quiz',
      'A finding becomes valuable when we can fix it. Choose a mitigation for each root cause. Defense is more than hiding a symptom: the trust boundary must be enforced.',
      'Choose a defense for authorization, output rendering, and database access.',
      'Enforce authorization on the server, encode untrusted output for its context, and bind database parameters. Retest fixes, update dependencies, and monitor for suspicious activity.', [
        q('Which patch should protect GET /api/profile/{id}?',
          ['Require login, then allow every numeric ID.', 'Hide the request from browser tools.', 'Check the caller\'s ownership or allowed role before returning the record.'], 2,
          'Authentication identifies a caller. Authorization checks what that caller may access.'),
        q('Which approach is safest when displaying an untrusted comment as plain text?',
          ['Insert it with a safe text API and context-aware output encoding.', 'Join it directly into HTML.', 'Reject only lowercase tags.'], 0,
          'A safe text sink treats the comment as data rather than interpreting it as markup.'),
        q('Which query approach keeps the username separate from SQL syntax?',
          ['Concatenate it after removing spaces.', 'Use a prepared statement with a bound username parameter.', 'Let the browser build the database query.'], 1,
          'Prepared statements with bound parameters preserve the intended query structure.')]),
    m('final', 'The citywide incident', 'Detective Byte', 'Internet Cafe', 'quiz',
      'The city receives a scholarship email leading to a fake portal. A separate training API exposes records, and unsafe query construction appears in the logs. Coordinate a response using everything you learned.',
      'Verify the message, investigate the file evidence, identify the API flaw, and select the repair plan.',
      'A complete response verifies sources, contains exposure, preserves evidence, fixes root causes, and retests. Report what happened clearly so the next citizen can avoid the same trap.', [
        q('The scholarship email links to academy-awards.example. The known portal is scholarships.academy.example. What comes first?',
          ['Sign in to see whether the form works.', 'Open the known portal independently and report the suspicious message.', 'Forward it to everyone to ask for help.'], 1,
          'Independent verification avoids trusting the suspicious message or spreading its link.'),
        q('An audit clue is in a hidden folder. Which sequence reveals it without executing unknown files?',
          ['ls -a, cd into the folder, then cat the evidence file.', 'Run every file until a flag appears.', 'Enter your password into the evidence file.'], 0,
          'List, navigate, and read. Investigation does not require executing unknown files.'),
        q('A signed-in user receives another person\'s record after changing its ID. Which repair matters?',
          ['A new logo.', 'A secret URL.', 'Server-side authorization for the requested record.'], 2,
          'Enforce object access on the server for every request, regardless of how the interface presents it.'),
        q('Which plan addresses the unsafe comments and concatenated queries, then verifies the repair?',
          ['Hide both forms and consider the case closed.', 'Use context-aware output encoding and bound parameters, then retest and monitor.', 'Ask users to avoid punctuation.'], 1,
          'Fix the rendering and query trust boundaries. Retest the same conditions and monitor for recurrence.')]),
    m('graduation', 'A citizen worth trusting', 'Professor Byte', 'Cyber Academy', 'quiz',
      'You followed the evidence, practiced the tools, and repaired the causes. The final lesson is a habit you can use every day: make trust a decision, not an assumption.',
      'Choose the response that combines verification, privacy, and reporting.',
      'Patrol complete. You are a CyberCity citizen defender. Pause, verify independently, protect your accounts, and help others recognize the warning signs.', [
        q('An unexpected message asks for your login code to claim an urgent reward. Which habit will you carry forward?',
          ['Pause, withhold the code, verify independently, and report the message.', 'Trust it if the logo looks familiar.', 'Share the code if the reward is valuable.'], 0,
          'Verification, limited disclosure, and reporting work together to protect both you and your community.')])
]

root = Path(__file__).resolve().parents[1] / 'Assets/CyberCity/Resources'
root.mkdir(parents=True, exist_ok=True)
text = json.dumps(dict(version=1, title='CyberCity Citizen Security Program', missions=missions), indent=2, ensure_ascii=True) + '\n'
(root / 'campaign.json').write_text(text, encoding='utf-8', newline='\n')
(root / 'campaign_hash.txt').write_text(hashlib.sha256(text.encode()).hexdigest() + '\n', encoding='utf-8', newline='\n')
print(f'Wrote {len(missions)} missions and SHA-256 checksum.')
